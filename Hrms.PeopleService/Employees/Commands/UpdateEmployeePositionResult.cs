using Hrms.PeopleService.Models.DTO;

namespace Hrms.PeopleService.Employees.Commands;

public enum UpdateEmployeePositionStatus
{
    Updated,
    NotFound,
    Inactive
}

public sealed record UpdateEmployeePositionResult(
    UpdateEmployeePositionStatus Status,
    EmployeeResponseDto? Employee = null);