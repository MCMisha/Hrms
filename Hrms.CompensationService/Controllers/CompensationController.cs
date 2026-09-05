using Hrms.CompensationService.Models;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("new")]
    public async Task<IActionResult> Post([FromBody] Compensation compensation)
    {
        await _context.Compensations.AddAsync(compensation);
        await _context.SaveChangesAsync();
        return Ok(compensation);
    }
    
}