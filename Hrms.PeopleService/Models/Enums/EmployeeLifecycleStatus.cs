namespace Hrms.PeopleService.Models.Enums;

public enum EmployeeLifecycleStatus
{
    Success,
    NotFound,
    InvalidDate,
    Conflict,
    DependencyUnavailable,
    DependencyTimeout,
    DependencyFailure,
    InvalidCompensationData,
    InvalidSalary
}