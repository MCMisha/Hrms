using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Microsoft.EntityFrameworkCore;

namespace Hrms.CompensationService.Services;

public class CompensationGrpcService(
    CompensationDbContext context)
    : CompensationGrpc.CompensationGrpcBase
{
    public override async Task<OpenCompensationResponse>
        OpenCompensation(
            OpenCompensationRequest request,
            ServerCallContext contextCall)
    {
        if (request.EmployeeId <= 0)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "EmployeeId is invalid"));
        }

        if (request.SalaryInGrosz <= 0)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Salary must be positive"));
        }

        if (request.EffectiveFrom is null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "EffectiveFrom is required"));
        }

        var effectiveFrom = request.EffectiveFrom.ToDateTime().Date;

        var latestCompensation = await context.Compensations
            .Where(c => c.EmployeeId == request.EmployeeId)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(contextCall.CancellationToken);

        if (latestCompensation is not null &&
            latestCompensation.EffectiveTo is null)
        {
            throw new RpcException(
                new Status(
                    StatusCode.AlreadyExists,
                    "Employee already has an active compensation."));
        }

        if (latestCompensation?.EffectiveTo is { } effectiveTo &&
            effectiveFrom <= effectiveTo.Date)
        {
            throw new RpcException(
                new Status(
                    StatusCode.FailedPrecondition,
                    "EffectiveFrom must be later than the previous EffectiveTo."));
        }

        var compensation = new Models.Compensation
        {
            EmployeeId = request.EmployeeId,
            BaseSalary = request.SalaryInGrosz / 100m,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null
        };

        context.Compensations.Add(compensation);
        await context.SaveChangesAsync(contextCall.CancellationToken);

        return new OpenCompensationResponse
        {
            CompensationId = compensation.Id
        };
    }

    public override async Task<CloseCurrentCompensationResponse> CloseCurrentCompensation(
        CloseCurrentCompensationRequest request, ServerCallContext contextCall)
    {
        if (request.EmployeeId <= 0)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "EmployeeId is invalid"));
        }

        if (request.EffectiveTo is null)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "EffectiveTo is required."));
        }
        
        var effectiveTo = request.EffectiveTo.ToDateTime().Date;

        var compensation = await context.Compensations
            .SingleOrDefaultAsync(
                compensation =>
                    compensation.EmployeeId == request.EmployeeId &&
                    compensation.EffectiveTo == null,
                contextCall.CancellationToken);

        if (compensation == null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound, "Employee is not found"));
        }

        if (effectiveTo.Date < compensation.EffectiveFrom.Date)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "EffectiveTo cannot precede EffectiveFrom."));
        }


        compensation.EffectiveTo = effectiveTo;
        context.Compensations.Update(compensation);

        await context.SaveChangesAsync();

        return new CloseCurrentCompensationResponse
        {
            CompensationId = compensation.Id
        };
    }
}