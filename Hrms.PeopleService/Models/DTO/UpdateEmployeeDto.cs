using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models.DTO;

public record UpdateEmployeeDto(
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Email,
    string Position,
    EmployeeStatus Status);