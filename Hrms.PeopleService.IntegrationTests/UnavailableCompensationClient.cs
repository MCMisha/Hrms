using Grpc.Core;
using Hrms.GrpcContracts.Compensation;

namespace Hrms.PeopleService.IntegrationTests;

sealed class UnavailableCompensationClient
    : CompensationGrpc.CompensationGrpcClient
{
    public override AsyncUnaryCall<CloseCurrentCompensationResponse>
        CloseCurrentCompensationAsync(
            CloseCurrentCompensationRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
    {
        throw new RpcException(
            new Status(StatusCode.Unavailable, "Compensation is unavailable."));
    }
}