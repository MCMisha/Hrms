using Hrms.PeopleService.Interfaces;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hrms.PeopleService.Services;

public class EmployeeLifecycleService : IEmployeeLifecycleService
{
    private readonly PeopleDbContext _context;
    private readonly ICompensationGateway _compensationGateway;

    public EmployeeLifecycleService(PeopleDbContext context, ICompensationGateway compensationGateway)
    {
        _context = context;
        _compensationGateway = compensationGateway;
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

        var result = await _compensationGateway.CloseAsync(
            employeeId,
            effectiveTo,
            cancellationToken);

        if (result.Status != CompensationStatus.Success)
        {
            var status = result.Status switch
            {
                CompensationStatus.NotFound or CompensationStatus.Conflict =>
                    EmployeeLifecycleStatus.Conflict,

                CompensationStatus.InvalidData =>
                    EmployeeLifecycleStatus.InvalidDate,

                CompensationStatus.Unavailable =>
                    EmployeeLifecycleStatus.DependencyUnavailable,

                CompensationStatus.Timeout =>
                    EmployeeLifecycleStatus.DependencyTimeout,

                _ => EmployeeLifecycleStatus.DependencyFailure
            };

            return new EmployeeLifecycleResult(status, employee);
        }

        employee.Status = EmployeeStatus.Inactive;
        await _context.SaveChangesAsync(cancellationToken);

        return new EmployeeLifecycleResult(
            EmployeeLifecycleStatus.Success,
            employee);
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

        var result = await _compensationGateway.OpenAsync(
            employee.Id,
            baseSalary,
            effectiveFrom,
            cancellationToken);
        if (result.Status != CompensationStatus.Success)
        {
            var status = result.Status switch
            {
                CompensationStatus.NotFound or CompensationStatus.Conflict =>
                    EmployeeLifecycleStatus.Conflict,
                
                CompensationStatus.InvalidSalary => EmployeeLifecycleStatus.InvalidSalary,

                CompensationStatus.InvalidData =>
                    EmployeeLifecycleStatus.InvalidCompensationData,

                CompensationStatus.Unavailable =>
                    EmployeeLifecycleStatus.DependencyUnavailable,

                CompensationStatus.Timeout =>
                    EmployeeLifecycleStatus.DependencyTimeout,

                _ => EmployeeLifecycleStatus.DependencyFailure
            };
            return new EmployeeLifecycleResult(status, employee);
        }
        
        employee.Status = EmployeeStatus.Active;
        await _context.SaveChangesAsync(cancellationToken);

        return new EmployeeLifecycleResult(EmployeeLifecycleStatus.Success, employee);
    }
}