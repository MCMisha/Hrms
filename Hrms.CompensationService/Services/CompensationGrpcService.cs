using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Google.Protobuf.WellKnownTypes;

namespace Hrms.CompensationService.Services;

public class CompensationGrpcService(
    CompensationDbContext context)
    : CompensationGrpc.CompensationGrpcBase
{
    public override async Task<CreateInitialCompensationResponse>
        CreateInitialCompensation(
            CreateInitialCompensationRequest request,
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

        var compensation = new Models.Compensation
        {
            EmployeeId = request.EmployeeId,
            BaseSalary = request.SalaryInGrosz / 100m,
            EffectiveFrom = request.EffectiveFrom.ToDateTime(),
            EffectiveTo = null
        };

        context.Compensations.Add(compensation);
        await context.SaveChangesAsync(contextCall.CancellationToken);

        return new CreateInitialCompensationResponse
        {
            CompensationId = compensation.Id
        };
    }
}