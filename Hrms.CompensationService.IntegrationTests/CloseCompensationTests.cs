using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Hrms.CompensationService.Models;
using Hrms.GrpcContracts.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.CompensationService.IntegrationTests;

public class CloseCompensationTests: IClassFixture<CompensationApiFactory>, IAsyncLifetime
{
    private readonly CompensationApiFactory _factory;

    public CloseCompensationTests(CompensationApiFactory factory)
    {
        _factory = factory;
    }
    
    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        await dbContext.Compensations.ExecuteDeleteAsync();
    }
    
    [Fact]
    public async Task CloseCurrentCompensation_WhenDateIsValid_SavesClosingDate()
    {
        // Arrange
        const int employeeId = 1010;

        var initialDate = new DateTime(
            2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var closeDate = new DateTime(
            2026, 10, 15, 12, 30, 0, DateTimeKind.Utc);

        var compensation = new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 5000m,
            EffectiveFrom = initialDate,
            EffectiveTo = null
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CompensationDbContext>();

            dbContext.Compensations.Add(compensation);
            await dbContext.SaveChangesAsync();
        }

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = _factory.Server.CreateHandler()
            });

        var grpcClient = new CompensationGrpc.CompensationGrpcClient(channel);

        var request = new CloseCurrentCompensationRequest
        {
            EmployeeId = employeeId,
            EffectiveTo = closeDate.ToTimestamp()
        };

        // Act
        var response = await grpcClient.CloseCurrentCompensationAsync(request);

        // Assert
        Assert.Equal(compensation.Id, response.CompensationId);

        await using var assertScope = _factory.Services.CreateAsyncScope();

        var assertDbContext = assertScope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        var savedCompensation = await assertDbContext.Compensations
            .AsNoTracking()
            .SingleAsync(c => c.Id == compensation.Id);

        Assert.Equal(closeDate.Date, savedCompensation.EffectiveTo);
        Assert.Equal(initialDate.Date, savedCompensation.EffectiveFrom);
        Assert.Equal(5000m, savedCompensation.BaseSalary);
    }

    [Fact]
    public async Task CloseCurrentCompensation_WhenDatePrecedesStart_ReturnsInvalidArgument()
    {
        // Arrange
        const int employeeId = 1011;

        var initialDate = new DateTime(
            2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

        var closeDate = initialDate.AddDays(-1);

        var compensation = new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 5000m,
            EffectiveFrom = initialDate,
            EffectiveTo = null
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CompensationDbContext>();

            dbContext.Compensations.Add(compensation);
            await dbContext.SaveChangesAsync();
        }

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = _factory.Server.CreateHandler()
            });

        var grpcClient = new CompensationGrpc.CompensationGrpcClient(channel);

        var request = new CloseCurrentCompensationRequest
        {
            EmployeeId = employeeId,
            EffectiveTo = closeDate.ToTimestamp()
        };

        // Act
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await grpcClient.CloseCurrentCompensationAsync(request));

        // Assert
        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);

        await using var assertScope = _factory.Services.CreateAsyncScope();

        var assertDbContext = assertScope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        var savedCompensation = await assertDbContext.Compensations
            .AsNoTracking()
            .SingleAsync(c => c.Id == compensation.Id);

        Assert.Null(savedCompensation.EffectiveTo);
        Assert.Equal(initialDate.Date, savedCompensation.EffectiveFrom);
        Assert.Equal(5000m, savedCompensation.BaseSalary);
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}