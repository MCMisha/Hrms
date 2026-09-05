using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models.DTO;

public record EmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Position,
    EmployeeStatus Status);