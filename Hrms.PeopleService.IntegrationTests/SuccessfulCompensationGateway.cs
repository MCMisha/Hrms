using Hrms.PeopleService.Interfaces;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.IntegrationTests;

public class SuccessfulCompensationGateway : ICompensationGateway
{
    public int OpenCallCount { get; private set; }
    public int? LastEmployeeId { get; private set; }
    public decimal? LastBaseSalary { get; private set; }
    public DateTime? LastEffectiveFrom { get; private set; }

    public Task<CompensationResult> CloseAsync(int employeeId, DateOnly effectiveTo,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new CompensationResult(CompensationStatus.Success));
    }

    public Task<CompensationResult> OpenAsync(int employeeId, decimal baseSalary, DateTime effectiveFrom,
        CancellationToken cancellationToken)
    {
        OpenCallCount++;
        LastEmployeeId = employeeId;
        LastBaseSalary = baseSalary;
        LastEffectiveFrom = effectiveFrom;
        return Task.FromResult(new CompensationResult(CompensationStatus.Success));
    }
}