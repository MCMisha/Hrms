namespace Hrms.PeopleService.Models.Enums;

public enum CompensationStatus
{
    Success,
    NotFound,
    InvalidSalary,
    InvalidData,
    Conflict,
    Unavailable,
    Timeout,
    Failure
}