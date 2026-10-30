using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Interfaces;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hrms.PeopleService.Services;

public class EmployeeLifecycleService : IEmployeeLifecycleService
{
    private readonly PeopleDbContext _context;
    private readonly CompensationGrpc.CompensationGrpcClient _client;

    public EmployeeLifecycleService(PeopleDbContext context, CompensationGrpc.CompensationGrpcClient client)
    {
        _context = context;
        _client = client;
    }

    public async Task<EmployeeLifecycleResult> DismissAsync(int employeeId, DateOnly effectiveTo,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(emp => emp.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.NotFound);
        }

        if (employee.Status == EmployeeStatus.Inactive)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.Conflict, employee);
        }

        if (effectiveTo > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidDate, employee);
        }

        var effectiveToDateTime = effectiveTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        try
        {
            await _client.CloseCurrentCompensationAsync(
                new CloseCurrentCompensationRequest
                {
                    EmployeeId = employee.Id,
                    EffectiveTo = Timestamp.FromDateTime(
                        effectiveToDateTime)
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            if (exception.StatusCode == StatusCode.Cancelled)
            {
                throw;
            }

            return exception.StatusCode switch
            {
                StatusCode.NotFound =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.Conflict, employee),

                StatusCode.InvalidArgument =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidDate, employee),

                StatusCode.Unavailable =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.DependencyUnavailable, employee),

                StatusCode.DeadlineExceeded =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.DependencyTimeout, employee),

                _ => new EmployeeLifecycleResult(
                    EmployeeLifecycleStatus.DependencyFailure)
            };
        }

        employee.Status = EmployeeStatus.Inactive;
        await _context.SaveChangesAsync(cancellationToken);

        return new EmployeeLifecycleResult(EmployeeLifecycleStatus.Success, employee);
    }

    public async Task<EmployeeLifecycleResult> RehireAsync(
        int employeeId,
        decimal baseSalary,
        DateTime effectiveFrom,
        CancellationToken cancellationToken)
    {
        if (baseSalary <= 0)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidSalary);
        }

        if (effectiveFrom.Kind != DateTimeKind.Utc ||
            effectiveFrom > DateTime.UtcNow)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidDate);
        }

        long salaryInGrosz;

        try
        {
            salaryInGrosz = decimal.ToInt64(
                decimal.Round(
                    baseSalary * 100m,
                    MidpointRounding.AwayFromZero));
        }
        catch (OverflowException)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidSalary);
        }

        if (salaryInGrosz <= 0)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidSalary);
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(
                e => e.Id == employeeId,
                cancellationToken);

        if (employee is null)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.NotFound);
        }

        if (employee.Status != EmployeeStatus.Inactive)
        {
            return new EmployeeLifecycleResult(EmployeeLifecycleStatus.Conflict);
        }

        try
        {
            await _client.OpenCompensationAsync(
                new OpenCompensationRequest
                {
                    EmployeeId = employee.Id,
                    SalaryInGrosz = salaryInGrosz,
                    EffectiveFrom = effectiveFrom.ToTimestamp()
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            if (exception.StatusCode == StatusCode.Cancelled)
            {
                throw;
            }

            return exception.StatusCode switch
            {
                StatusCode.InvalidArgument =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.InvalidCompensationData),

                StatusCode.AlreadyExists or StatusCode.FailedPrecondition =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.Conflict),

                StatusCode.Unavailable =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.DependencyUnavailable),

                StatusCode.DeadlineExceeded =>
                    new EmployeeLifecycleResult(EmployeeLifecycleStatus.DependencyTimeout),

                _ => new EmployeeLifecycleResult(EmployeeLifecycleStatus.DependencyFailure)
            };
        }

        employee.Status = EmployeeStatus.Active;
        await _context.SaveChangesAsync(cancellationToken);

        return new EmployeeLifecycleResult(EmployeeLifecycleStatus.Success, employee);
    }
}