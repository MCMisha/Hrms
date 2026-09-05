namespace Hrms.CompensationService.Models;

public class Compensation
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal BaseSalary { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}