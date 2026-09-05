using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models.Requests;

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Email,
    string Position,
    EmployeeStatus Status,
    decimal InitialSalary,
    DateTimeOffset SalaryEffectiveFrom);