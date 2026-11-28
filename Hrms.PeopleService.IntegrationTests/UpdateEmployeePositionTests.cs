using System.Net;
using System.Net.Http.Json;
using Hrms.PeopleService.Employees.Commands;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.PeopleService.IntegrationTests;

public class UpdateEmployeePositionTests
    : IClassFixture<PeopleApiFactory>, IDisposable
{
    private readonly PeopleApiFactory _factory;
    private readonly HttpClient _client;

    public UpdateEmployeePositionTests(PeopleApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdatePosition_WhenRouteAndBodyIdsDiffer_UpdatesEmployeeFromRoute()
    {
        // Arrange
        var existingEmployee = new Employee
        {
            DateOfBirth = new DateTime(1989, 1, 1),
            FirstName = "John",
            LastName = "Doe",
            Email = "position.route@mail.com",
            Position = "Test position",
            Status = EmployeeStatus.Active
        };

        var otherEmployee = new Employee
        {
            DateOfBirth = new DateTime(1989, 1, 1),
            FirstName = "Mark",
            LastName = "Snow",
            Email = "position.other@mail.com",
            Position = "Junior Developer",
            Status = EmployeeStatus.Active
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PeopleDbContext>();

            db.Employees.AddRange(existingEmployee, otherEmployee);
            await db.SaveChangesAsync();
        }

        var requestBody = new UpdateEmployeePositionCommand(
            otherEmployee.Id,
            "Test position updated");

        // Act
        using var response = await _client.PatchAsJsonAsync(
            $"/Employee/{existingEmployee.Id}/position",
            requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var assertScope = _factory.Services.CreateAsyncScope();

        var assertDb = assertScope.ServiceProvider
            .GetRequiredService<PeopleDbContext>();

        var target = await assertDb.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == existingEmployee.Id);

        var other = await assertDb.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == otherEmployee.Id);

        Assert.Equal("Test position updated", target.Position);
        Assert.Equal("Junior Developer", other.Position);
    }

    [Fact]
    public async Task UpdatePosition_WhenEmployeeIsNotActive_ReturnsConflictWithNoChanges()
    {
        // Arrange
        var existingEmployee = new Employee
        {
            DateOfBirth = new DateTime(1989, 1, 1),
            FirstName = "John",
            LastName = "Doe",
            Email = "position.inactive@mail.com",
            Position = "Test position",
            Status = EmployeeStatus.Inactive
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PeopleDbContext>();

            db.Employees.Add(existingEmployee);
            await db.SaveChangesAsync();
        }

        var requestBody = new UpdateEmployeePositionCommand(
            existingEmployee.Id,
            "Test position updated");

        // Act
        using var response = await _client.PatchAsJsonAsync(
            $"/Employee/{existingEmployee.Id}/position",
            requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var assertScope = _factory.Services.CreateAsyncScope();

        var assertDb = assertScope.ServiceProvider
            .GetRequiredService<PeopleDbContext>();

        var savedEmployee = await assertDb.Employees
            .AsNoTracking()
            .SingleAsync(e => e.Id == existingEmployee.Id);

        Assert.Equal("Test position", savedEmployee.Position);
        Assert.Equal(EmployeeStatus.Inactive, savedEmployee.Status);
    }

    [Fact]
    public async Task UpdatePosition_WhenEmployeeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int employeeId = 1300;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PeopleDbContext>();

            Assert.False(
                await db.Employees.AnyAsync(e => e.Id == employeeId));
        }

        var requestBody = new UpdateEmployeePositionCommand(
            employeeId,
            "Test position updated");

        // Act
        using var response = await _client.PatchAsJsonAsync(
            $"/Employee/{employeeId}/position",
            requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}