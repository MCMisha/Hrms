using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Interfaces;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Services;

public class CompensationGateway : ICompensationGateway
{
    private readonly CompensationGrpc.CompensationGrpcClient _client;

    public CompensationGateway(CompensationGrpc.CompensationGrpcClient client)
    {
        _client = client;
    }

    public async Task<CompensationResult> CloseAsync(int employeeId, DateOnly effectiveTo,
        CancellationToken cancellationToken)
    {
        var effectiveToDateTime = effectiveTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        try
        {
            await _client.CloseCurrentCompensationAsync(
                new CloseCurrentCompensationRequest
                {
                    EmployeeId = employeeId,
                    EffectiveTo = Timestamp.FromDateTime(
                        effectiveToDateTime)
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            if (exception.StatusCode == StatusCode.Cancelled &&
                cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return exception.StatusCode switch
            {
                StatusCode.NotFound =>
                    new CompensationResult(CompensationStatus.NotFound),

                StatusCode.InvalidArgument =>
                    new CompensationResult(CompensationStatus.InvalidData),

                StatusCode.Unavailable =>
                    new CompensationResult(CompensationStatus.Unavailable),

                StatusCode.DeadlineExceeded =>
                    new CompensationResult(CompensationStatus.Timeout),

                _ => new CompensationResult(
                    CompensationStatus.Failure)
            };
        }

        return new CompensationResult(CompensationStatus.Success);
    }

    public async Task<CompensationResult> OpenAsync(int employeeId, decimal baseSalary, DateTime effectiveFrom,
        CancellationToken cancellationToken)
    {
        if (effectiveFrom.Kind != DateTimeKind.Utc)
        {
            return new CompensationResult(CompensationStatus.InvalidData);
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
            return new CompensationResult(CompensationStatus.InvalidSalary);
        }

        if (salaryInGrosz <= 0)
        {
            return new CompensationResult(CompensationStatus.InvalidSalary);
        }
        
        try
        {
            await _client.OpenCompensationAsync(
                new OpenCompensationRequest
                {
                    EmployeeId = employeeId,
                    SalaryInGrosz = salaryInGrosz,
                    EffectiveFrom = effectiveFrom.ToTimestamp()
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            if (exception.StatusCode == StatusCode.Cancelled &&
                cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return exception.StatusCode switch
            {
                StatusCode.InvalidArgument =>
                    new CompensationResult(CompensationStatus.InvalidData),

                StatusCode.AlreadyExists or StatusCode.FailedPrecondition =>
                    new CompensationResult(CompensationStatus.Conflict),

                StatusCode.Unavailable =>
                    new CompensationResult(CompensationStatus.Unavailable),

                StatusCode.DeadlineExceeded =>
                    new CompensationResult(CompensationStatus.Timeout),

                _ => new CompensationResult(CompensationStatus.Failure)
            };
        }
        return new CompensationResult(CompensationStatus.Success);
    }
}