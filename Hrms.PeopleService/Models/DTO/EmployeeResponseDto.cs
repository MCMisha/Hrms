using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models.DTO;

public record EmployeeResponseDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Position,
    EmployeeStatus Status);