using System.Net;
using System.Net.Http.Json;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Hrms.PeopleService.IntegrationTests;

public class SearchEmployeesTests : IClassFixture<PeopleApiFactory>,
    IDisposable
{
    private readonly PeopleApiFactory _factory;
    private readonly HttpClient _client;

    public SearchEmployeesTests( PeopleApiFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SearchEmployeesByNameAndStatus()
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
            Status = EmployeeStatus.Inactive
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PeopleDbContext>();

            db.Employees.AddRange(existingEmployee, otherEmployee);
            await db.SaveChangesAsync();
        }

        string name = "jOhN";
        EmployeeStatus status = EmployeeStatus.Active;
        
        using var response = await _client.GetAsync(
            $"/Employee?search={name}&status={(int)status}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var employees = await response.Content
            .ReadFromJsonAsync<List<Employee>>();

        Assert.NotNull(employees);

        var employee = Assert.Single(employees);

        Assert.Equal(existingEmployee.Id, employee.Id);
        Assert.Equal(EmployeeStatus.Active, employee.Status);
    }
    
    [Fact]
    public async Task Search_WhenNoMatches_ReturnsEmptyArray()
    {
        // Arrange
        var employee = new Employee
        {
            DateOfBirth = new DateTime(1989, 1, 1),
            FirstName = "Mark",
            LastName = "Snow",
            Email = "search.no.matches@mail.com",
            Position = "Junior Developer",
            Status = EmployeeStatus.Active
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PeopleDbContext>();

            db.Employees.Add(employee);
            await db.SaveChangesAsync();
        }

        // Act
        using var response = await _client.GetAsync(
            "/Employee?search=NoSuchEmployeeName");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employees = await response.Content
            .ReadFromJsonAsync<List<Employee>>();

        Assert.NotNull(employees);
        Assert.Empty(employees);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }
}