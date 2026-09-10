namespace Hrms.CompensationService.Models.DTO;

public record ChangeCompensationDto(int EmployeeId, decimal BaseSalary, DateTime EffectiveFrom);