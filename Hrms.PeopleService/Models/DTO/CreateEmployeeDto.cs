namespace Hrms.PeopleService.Models.DTO;

public record CreateEmployeeDto(
    string FirstName,
    string LastName,
    string Email,
    DateTime DateOfBirth,
    string Position,
    decimal InitialSalary);