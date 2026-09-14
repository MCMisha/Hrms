using System.Globalization;
using Hrms.CompensationService.Models;
using Hrms.CompensationService.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hrms.CompensationService.Controllers;

[ApiController]
[Route("[controller]")]
public class CompensationController : ControllerBase
{
    private readonly ILogger<CompensationController> _logger;
    private readonly CompensationDbContext _context;

    public CompensationController(ILogger<CompensationController> logger, CompensationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet("by-id/{id}")]
    public IActionResult GetById(int id)
    {
        Compensation? compensation = _context.Compensations.FirstOrDefault(x => x.Id == id);
        if (compensation == null)
        {
            return NotFound();
        }

        return Ok(compensation);
    }

    [HttpGet("employee/{employeeId:int}/at")]
    public async Task<IActionResult> GetByEmployeeId(
        int employeeId,
        [FromQuery] string date,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dateTime))
        {
            return BadRequest("Date must have format yyyy-MM-dd");
        }

        Compensation? compensation = await _context.Compensations
            .SingleOrDefaultAsync(
                c => c.EmployeeId == employeeId
                     && c.EffectiveFrom <= dateTime
                     && (c.EffectiveTo == null || c.EffectiveTo >= dateTime),
                cancellationToken);

        if (compensation == null)
        {
            return NotFound();
        }

        return Ok(compensation);
    }

    [HttpPost("new")]
    public async Task<IActionResult> Post([FromBody] Compensation compensation)
    {
        await _context.Compensations.AddAsync(compensation);
        await _context.SaveChangesAsync();
        return Ok(compensation);
    }

    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] ChangeCompensationDto changeCompensationDto,
        CancellationToken cancellationToken)
    {
        if (changeCompensationDto.BaseSalary <= 0)
        {
            return BadRequest();
        }

        var existingCompensation =
            await _context.Compensations.SingleOrDefaultAsync(
                c => c.EffectiveTo == null && c.EmployeeId == changeCompensationDto.EmployeeId, cancellationToken);
        if (existingCompensation == null)
        {
            return NotFound();
        }

        if (changeCompensationDto.EffectiveFrom.Date < existingCompensation.EffectiveFrom.Date)
        {
            return BadRequest();
        }

        var updatedCompensation = new Compensation
        {
            EmployeeId = changeCompensationDto.EmployeeId,
            BaseSalary = changeCompensationDto.BaseSalary,
            EffectiveFrom = changeCompensationDto.EffectiveFrom.Date,
            EffectiveTo = null
        };
        existingCompensation.EffectiveTo = changeCompensationDto.EffectiveFrom.AddDays(-1);
        _context.Compensations.Add(updatedCompensation);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok("Compensation updated");
    }

    [HttpGet("employee/{employeeId}/history")]
    public async Task<IActionResult> EmployeeCompensationHistory(int employeeId, CancellationToken cancellationToken)
    {
        var compensationsByEmployee = await _context.Compensations
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.EffectiveFrom)
            .ToListAsync(cancellationToken);
        
        return Ok(compensationsByEmployee);
    }
}