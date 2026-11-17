using Grpc.Core;
using Hrms.GrpcContracts.Compensation;

namespace Hrms.PeopleService.IntegrationTests;

public class FailedPreconditionCompensationClient : CompensationGrpc.CompensationGrpcClient
{
    public int CallCount { get; private set; }
    public override AsyncUnaryCall<OpenCompensationResponse> OpenCompensationAsync(
        OpenCompensationRequest request,
        Metadata? headers = null,
        DateTime? deadline = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        throw new RpcException(new Status(
            StatusCode.FailedPrecondition,
            "Compensation periods overlap."));
    }
}