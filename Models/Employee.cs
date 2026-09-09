using Hrms.Models.Enums;

namespace Hrms.Models;

public record Employee(int Id,  string FirstName, string LastName, DateTime DateOfBirth,
    string Email, string Position, decimal Salary, EmployeeStatus Status);