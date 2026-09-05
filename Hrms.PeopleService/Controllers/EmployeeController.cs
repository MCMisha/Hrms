using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;
using Hrms.PeopleService.Models.Enums;
using Hrms.PeopleService.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.PeopleService.Controllers;

[ApiController]
[Route("[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly PeopleDbContext _context;
    private readonly Channel<ImportJob> _channel;
    private readonly ImportJobStore _jobStore;
    private readonly CompensationGrpc.CompensationGrpcClient _compensationClient;

    public EmployeeController(PeopleDbContext context,
        Channel<ImportJob> channel, ImportJobStore importJobStore,
        CompensationGrpc.CompensationGrpcClient compensationClient)
    {
        _context = context;
        _channel = channel;
        _jobStore = importJobStore;
        _compensationClient = compensationClient;
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
    public async Task<IActionResult> Post(
        [FromBody] CreateEmployeeDto request,
        CancellationToken cancellationToken)
    {
        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            Position = request.Position,
            Status = EmployeeStatus.Active
        };

        await _context.Employees.AddAsync(employee, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        await _compensationClient.CreateInitialCompensationAsync(
            new CreateInitialCompensationRequest
            {
                EmployeeId = employee.Id,
                SalaryInGrosz = decimal.ToInt64(
                    decimal.Round(
                        request.InitialSalary * 100m,
                        MidpointRounding.AwayFromZero)),
                EffectiveFrom = Timestamp.FromDateTimeOffset(
                    DateTime.Now)
            },
            cancellationToken: cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { id = employee.Id },
            employee);
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


    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] UpdateEmployeeDto request,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees.FindAsync(
            [id],
            cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.DateOfBirth = request.DateOfBirth;
        employee.Email = request.Email;
        employee.Position = request.Position;
        employee.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

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