using System.Net;
using System.Net.Http.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;
using Hrms.PeopleService.Models.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hrms.PeopleService.IntegrationTests;

public class CompensationServiceTests
    : IClassFixture<PeopleApiFactory>, IAsyncLifetime
{
    private readonly PeopleApiFactory _factory;
    private readonly HttpClient _client;

    public CompensationServiceTests(PeopleApiFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CompensationGrpc.CompensationGrpcClient>();
                services.AddSingleton<CompensationGrpc.CompensationGrpcClient>(
                    new UnavailableCompensationClient());
            });
        });
    }

    [Fact]
    public async Task NoChanges_WhenDismissEmployeeWithUnavailableCompensation()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CompensationGrpc.CompensationGrpcClient>();
                services.AddSingleton<CompensationGrpc.CompensationGrpcClient>(
                    new UnavailableCompensationClient());
            });
        });
        var dateOfBirth = new DateTime(1990, 01, 01);
        var existingEmployee = new Employee
        {
            DateOfBirth = dateOfBirth,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@mail.com",
            Position = "Test position",
            Status = EmployeeStatus.Active
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var peopleDbContext = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            peopleDbContext.Employees.Add(existingEmployee);
            await peopleDbContext.SaveChangesAsync();
        }

        var employeeId = existingEmployee.Id;

        using var client = factory.CreateClient();

        // Act
        using var response = await client.PatchAsJsonAsync(
            $"/Employee/{employeeId}/dismiss",
            new { effectiveTo = DateOnly.FromDateTime(DateTime.UtcNow) });

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        var savedEmployee = await db.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == employeeId);

        Assert.Equal(EmployeeStatus.Active, savedEmployee.Status);
    }
    
    [Fact]
    public async Task Rehire_WhenEmployeeIsAlreadyActive_ReturnsConflict()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CompensationGrpc.CompensationGrpcClient>();
                services.AddSingleton<CompensationGrpc.CompensationGrpcClient>(
                    new UnavailableCompensationClient());
            });
        });
        var dateOfBirth = new DateTime(1989, 01, 01);
        var existingEmployee = new Employee
        {
            DateOfBirth = dateOfBirth,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@mail.com",
            Position = "Test position",
            Status = EmployeeStatus.Active
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var peopleDbContext = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            peopleDbContext.Employees.Add(existingEmployee);
            await peopleDbContext.SaveChangesAsync();
        }

        var employeeId = existingEmployee.Id;

        using var client = factory.CreateClient();
        RehireEmployeeDto requestBody = new RehireEmployeeDto(1000, 
            new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc));
        // Act
        using var response = await client.PostAsJsonAsync(
            $"/Employee/{employeeId}/rehire",
            requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        var savedEmployee = await db.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == employeeId);

        Assert.Equal(EmployeeStatus.Active, savedEmployee.Status);
    }
    
    [Fact]
    public async Task Rehire_WhenCompensationRejectsRequest_ReturnsConflict()
    {
        var compensationClient = new FailedPreconditionCompensationClient();
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CompensationGrpc.CompensationGrpcClient>();
                services.AddSingleton<CompensationGrpc.CompensationGrpcClient>(compensationClient);
            });
        });
        var dateOfBirth = new DateTime(1989, 01, 01);
        var existingEmployee = new Employee
        {
            DateOfBirth = dateOfBirth,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@mail.com",
            Position = "Test position",
            Status = EmployeeStatus.Inactive
        };
        
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var peopleDbContext = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            peopleDbContext.Employees.Add(existingEmployee);
            await peopleDbContext.SaveChangesAsync();
        }

        var employeeId = existingEmployee.Id;
        using var client = factory.CreateClient();

        RehireEmployeeDto requestBody = new RehireEmployeeDto(1000, 
            new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc));
        // Act
        using var response = await client.PostAsJsonAsync(
            $"/Employee/{employeeId}/rehire",
            requestBody);
        
        // Assert
        Assert.Equal(1, compensationClient.CallCount);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        
        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        var savedEmployee = await db.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == employeeId);
        Assert.Equal(EmployeeStatus.Inactive, savedEmployee.Status);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}