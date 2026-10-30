using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models;

public record EmployeeLifecycleResult(
    EmployeeLifecycleStatus Status,
    Employee? Employee = null);