using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models;

public class Employee
{
    public int Id { get; init; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public required string Email { get; set; }
    public required string Position { get; set; }
    public EmployeeStatus Status { get; set; }
}