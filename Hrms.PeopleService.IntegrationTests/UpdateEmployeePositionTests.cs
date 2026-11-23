using System.Net;
using System.Net.Http.Json;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Employees.Commands;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hrms.PeopleService.IntegrationTests;

public class UpdateEmployeePositionTests : IClassFixture<PeopleApiFactory>, IAsyncLifetime
{
    private readonly PeopleApiFactory _factory;
    private readonly HttpClient _client;

    public UpdateEmployeePositionTests(PeopleApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task UpdatePosition_WhenRouteAndBodyIdsDiffer_UpdatesEmployeeFromRoute()
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
            Status = EmployeeStatus.Active
        };

        var existingEmployee2 = new Employee
        {
            DateOfBirth = dateOfBirth,
            FirstName = "Mark",
            LastName = "Snow",
            Email = "mark.snow@mail.com",
            Position = "Junior Developer",
            Status = EmployeeStatus.Active
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var peopleDbContext = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            peopleDbContext.Employees.Add(existingEmployee);
            peopleDbContext.Employees.Add(existingEmployee2);
            await peopleDbContext.SaveChangesAsync();
        }

        var employeeId = existingEmployee.Id;
        var employeeId2 = existingEmployee2.Id;

        var requestBody = new UpdateEmployeePositionCommand(employeeId2, "Test position updated");
        using var response = await _client.PatchAsJsonAsync($"Employee/{employeeId}/position", requestBody);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        var target = await db.Employees.AsNoTracking()
            .SingleAsync(e => e.Id == employeeId);
        var other = await db.Employees.AsNoTracking()
            .SingleAsync(e => e.Id == employeeId2);

        Assert.Equal("Test position updated", target.Position);
        Assert.Equal("Junior Developer", other.Position);
    }

    [Fact]
    public async Task UpdatePosition_WhenEmployeeIsNotActive_ReturnsConflictWithNoChanges()
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
        
        var requestBody = new UpdateEmployeePositionCommand(employeeId, "Test position updated");
        using var response = await _client.PatchAsJsonAsync($"Employee/{employeeId}/position", requestBody);
        
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        
        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<PeopleDbContext>();
        var target = await db.Employees.AsNoTracking()
            .SingleAsync(e => e.Id == employeeId);
        
        Assert.Equal("Test position", target.Position);
    }

    [Fact]
    public async Task UpdatePosition_WhenEmployeeDoesNotExist_ReturnsNotFound()
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
        var employeeId = 1300; 
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            Assert.False(await db.Employees.AnyAsync(e => e.Id == employeeId));
        }
        
        var requestBody = new UpdateEmployeePositionCommand(employeeId, "Test position updated");
        using var response = await _client.PatchAsJsonAsync($"Employee/{employeeId}/position", requestBody);
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}