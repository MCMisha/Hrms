using System.Threading.Channels;
using DataPipeline.Core.Builders;
using DataPipeline.Core.Interfaces;
using DataPipeline.Core.Runners;
using DataPipeline.Csv;
using Hrms.Mappers;
using Hrms.Models;
using Hrms.Models.Enums;
using Hrms.Sinks;
using Hrms.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Controllers;

[ApiController]
[Route("[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly ILogger<EmployeeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly Channel<ImportJob> _channel;
    private readonly ImportJobStore _jobStore;

    public EmployeeController(ILogger<EmployeeController> logger, IConfiguration configuration, AppDbContext context,
        Channel<ImportJob> channel, ImportJobStore importJobStore)
    {
        _logger = logger;
        _configuration = configuration;
        _context = context;
        _channel = channel;
        _jobStore = importJobStore;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(_context.Employees.ToList());
    }

    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        Employee? employee = _context.Employees.FirstOrDefault(x => x.Id == id);
        if (employee == null)
        {
            return NotFound();
        }

        return Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Employee employee)
    {
        await _context.Employees.AddAsync(employee);
        await _context.SaveChangesAsync();
        return Ok(employee);
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest("File is empty.");
        }
        var jobId = Guid.NewGuid();

        var filePath = Path.Combine(
            "imports",
            $"{jobId}.csv");

        Directory.CreateDirectory("imports");

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(
                stream,
                cancellationToken);    
        }

        var job = new ImportJob(
            jobId,
            filePath);

        _jobStore.Add(jobId);
        await _channel.Writer.WriteAsync(
            job,
            cancellationToken);

        return Accepted(new
        {
            JobId = jobId,
            Status = ImportJobStatus.Pending
        });
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] Employee employee)
    {
        Employee? employeeExisted = _context.Employees.FirstOrDefault(x => x.Id == id);
        if (employeeExisted == null)
        {
            return NotFound();
        }

        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
        return Ok(employee);
    }
    
    [HttpGet("import/{jobId:guid}")]
    public IActionResult GetImportStatus(Guid jobId)
    {
        var job = _jobStore.Get(jobId);

        if (job is null)
            return NotFound();

        return Ok(job);
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        Employee? employee = _context.Employees.FirstOrDefault(employee => employee.Id == id);
        if (employee == null)
        {
            return NotFound();
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();
        return Ok(employee);
    }
}