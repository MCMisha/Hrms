using Hrms.PeopleService.Models;

namespace Hrms.PeopleService.Interfaces;

public interface IEmployeeLifecycleService
{
    Task<EmployeeLifecycleResult> DismissAsync(
        int employeeId,
        DateOnly effectiveTo,
        CancellationToken cancellationToken);

    Task<EmployeeLifecycleResult> RehireAsync(int employeeId,
        decimal baseSalary,
        DateTime effectiveFrom,
        CancellationToken cancellationToken
    );
}