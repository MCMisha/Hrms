using Hrms.PeopleService.Models;

namespace Hrms.PeopleService.Interfaces;

public interface ICompensationGateway
{
    Task<CompensationResult> CloseAsync(
        int employeeId,
        DateOnly effectiveTo,
        CancellationToken cancellationToken);

    Task<CompensationResult> OpenAsync(
        int employeeId,
        decimal baseSalary,
        DateTime effectiveFrom,
        CancellationToken cancellationToken);
}