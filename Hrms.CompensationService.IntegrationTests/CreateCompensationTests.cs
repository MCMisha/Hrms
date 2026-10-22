using System.Net.Http.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Hrms.CompensationService.Models;
using Hrms.CompensationService.Models.DTO;
using Hrms.GrpcContracts.Compensation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hrms.CompensationService.IntegrationTests;

public sealed class CreateCompensationTests
    : IClassFixture<CompensationApiFactory>, IAsyncLifetime
{
    private readonly CompensationApiFactory _factory;
    private readonly HttpClient _client;

    public CreateCompensationTests(CompensationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    
    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        await dbContext.Compensations.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Post_WhenDataIsValid_SavesActiveCompensation()
    {
        var request = new Compensation
        {
            EmployeeId = 1001,
            BaseSalary = 5000m,
            EffectiveFrom = new DateTime(2026, 10, 17),
            EffectiveTo = null
        };

        var response = await _client.PostAsJsonAsync(
            "/Compensation/new",
            request);

        response.EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        var savedCompensation = await dbContext.Compensations
            .AsNoTracking()
            .SingleAsync(c => c.EmployeeId == request.EmployeeId);

        Assert.Equal(request.BaseSalary, savedCompensation.BaseSalary);
        Assert.Equal(request.EffectiveFrom, savedCompensation.EffectiveFrom);
        Assert.Null(savedCompensation.EffectiveTo);
    }

    [Fact]
    public async Task Put_WhenDataIsValid_SavesActiveCompensation()
    {
        const int employeeId = 1002;

        var initialDate = new DateTime(
            2026, 10, 17,
            0, 0, 0,
            DateTimeKind.Utc);

        var changeDate = new DateTime(
            2026, 12, 1,
            0, 0, 0,
            DateTimeKind.Utc);

        var initialCompensation = new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 5000m,
            EffectiveFrom = initialDate,
            EffectiveTo = null
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/Compensation/new",
            initialCompensation);

        createResponse.EnsureSuccessStatusCode();

        var updateRequest = new ChangeCompensationDto(
            employeeId,
            7000m,
            changeDate);

        // Act
        var updateResponse = await _client.PutAsJsonAsync(
            "/Compensation/update",
            updateRequest);

        updateResponse.EnsureSuccessStatusCode();

        // Assert
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        var compensations = await dbContext.Compensations
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderBy(c => c.EffectiveFrom)
            .ToListAsync();

        Assert.Equal(2, compensations.Count);

        var previousCompensation = compensations[0];
        var currentCompensation = compensations[1];

        Assert.Equal(5000m, previousCompensation.BaseSalary);
        Assert.Equal(
            changeDate.AddDays(-1),
            previousCompensation.EffectiveTo);

        Assert.Equal(7000m, currentCompensation.BaseSalary);
        Assert.Equal(changeDate, currentCompensation.EffectiveFrom);
        Assert.Null(currentCompensation.EffectiveTo);
    }
    
    [Fact]
    public async Task Database_WhenSecondActiveCompensationIsAdded_ThrowsUniqueViolation()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        const int employeeId = 1003;

        dbContext.Compensations.Add(new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 5000m,
            EffectiveFrom = new DateTime(
                2026, 10, 17,
                0, 0, 0,
                DateTimeKind.Utc),
            EffectiveTo = null
        });

        await dbContext.SaveChangesAsync();

        dbContext.Compensations.Add(new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 7000m,
            EffectiveFrom = new DateTime(
                2026, 12, 1,
                0, 0, 0,
                DateTimeKind.Utc),
            EffectiveTo = null
        });

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());

        // Assert
        var postgresException =
            Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);
    }
    
    [Fact]
    public async Task CloseCurrentCompensation_WhenActiveCompensationExists_ClosesIt()
    {
        // Arrange
        const int employeeId = 1004;

        var closeDate = new DateTime(
            2026, 10, 18,
            12, 0, 0,
            DateTimeKind.Utc);

        var initialCompensation = new Compensation
        {
            EmployeeId = employeeId,
            BaseSalary = 5000m,
            EffectiveFrom = closeDate.AddMonths(-1),
            EffectiveTo = null
        };

        await using (var arrangeScope =
                     _factory.Services.CreateAsyncScope())
        {
            var dbContext = arrangeScope.ServiceProvider
                .GetRequiredService<CompensationDbContext>();

            dbContext.Compensations.Add(initialCompensation);
            await dbContext.SaveChangesAsync();
        }

        var expectedCompensationId = initialCompensation.Id;

        var handler = _factory.Server.CreateHandler();

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = handler
            });

        var grpcClient =
            new CompensationGrpc.CompensationGrpcClient(channel);

        var request = new CloseCurrentCompensationRequest
        {
            EmployeeId = employeeId,
            EffectiveTo = closeDate.ToTimestamp()
        };

        // Act
        var response = await grpcClient
            .CloseCurrentCompensationAsync(request);

        // Assert — ответ gRPC
        Assert.Equal(
            expectedCompensationId,
            response.CompensationId);

        // Assert — фактическое состояние PostgreSQL
        await using var assertScope =
            _factory.Services.CreateAsyncScope();

        var assertDbContext = assertScope.ServiceProvider
            .GetRequiredService<CompensationDbContext>();

        var savedCompensation = await assertDbContext.Compensations
            .AsNoTracking()
            .SingleAsync(c => c.Id == expectedCompensationId);

        Assert.Equal(employeeId, savedCompensation.EmployeeId);
        Assert.Equal(closeDate, savedCompensation.EffectiveTo);
    }
    
    [Fact]
    public async Task CloseCurrentCompensation_WhenNoActiveCompensationExists_ReturnsNotFound()
    {
        // Arrange
        const int employeeId = 1005;

        var closeDate = new DateTime(
            2026, 10, 18,
            12, 0, 0,
            DateTimeKind.Utc);

        var handler = _factory.Server.CreateHandler();

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = handler
            });

        var grpcClient =
            new CompensationGrpc.CompensationGrpcClient(channel);

        var request = new CloseCurrentCompensationRequest
        {
            EmployeeId = employeeId,
            EffectiveTo = closeDate.ToTimestamp()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await grpcClient
            .CloseCurrentCompensationAsync(request));
        
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}