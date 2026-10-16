using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;
using Hrms.PeopleService.Models.Enums;
using Hrms.PeopleService.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        Employee? employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
        if (employee == null)
        {
            return NotFound();
        }

        return Ok(employee);
    }

    [HttpGet("{id:int}/exists")]
    public async Task<IActionResult> Exists(int id, CancellationToken cancellationToken)
    {
        bool exists = await _context.Employees.AnyAsync(x => x.Id == id, cancellationToken);
        return Ok(exists);
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
            Status = EmployeeStatus.Onboarding
        };

        await _context.Employees.AddAsync(employee, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _compensationClient.CreateInitialCompensationAsync(
                new CreateInitialCompensationRequest
                {
                    EmployeeId = employee.Id,
                    SalaryInGrosz = decimal.ToInt64(
                        decimal.Round(
                            request.InitialSalary * 100m,
                            MidpointRounding.AwayFromZero)),
                    EffectiveFrom = Timestamp.FromDateTimeOffset(
                        DateTime.UtcNow)
                },
                cancellationToken: cancellationToken);
            employee.Status = EmployeeStatus.Active;
            _context.Employees.Update(employee);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (RpcException ex)
        {
            if (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
            {
                throw;
            }

            return ex.StatusCode switch
            {
                Grpc.Core.StatusCode.InvalidArgument
                    => BadRequest("Invalid compensation data."),

                Grpc.Core.StatusCode.AlreadyExists
                    => Conflict("Initial compensation already exists."),

                Grpc.Core.StatusCode.FailedPrecondition
                    => Conflict("Compensation cannot be created in the current state."),

                Grpc.Core.StatusCode.Unavailable
                    => StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        "Compensation service is temporarily unavailable."),

                Grpc.Core.StatusCode.DeadlineExceeded
                    => StatusCode(
                        StatusCodes.Status504GatewayTimeout,
                        "Compensation service did not respond in time."),
                _
                    => StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "An error occurred while creating initial compensation.")
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }

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

    [HttpPatch("{id}/dismiss")]
    public async Task<IActionResult> Dismiss(int id, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(emp => emp.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Status == EmployeeStatus.Inactive)
        {
            return Conflict("Employee is already inactive.");
        }

        employee.Status = EmployeeStatus.Inactive;
        await _context.SaveChangesAsync(cancellationToken);
        
        try
        {
            await _compensationClient.CloseCurrentCompensationAsync(
                new CloseCurrentCompensationRequest
                {
                    EmployeeId = employee.Id,
                    EffectiveTo = Timestamp.FromDateTime(
                        DateTime.UtcNow)
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            return exception.StatusCode switch
            {
                Grpc.Core.StatusCode.NotFound =>
                    Conflict("Employee has no active compensation."),

                Grpc.Core.StatusCode.InvalidArgument =>
                    BadRequest("Invalid compensation closing date."),

                Grpc.Core.StatusCode.Unavailable =>
                    StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        "Compensation service is temporarily unavailable."),

                Grpc.Core.StatusCode.DeadlineExceeded =>
                    StatusCode(
                        StatusCodes.Status504GatewayTimeout,
                        "Compensation service did not respond in time."),

                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while closing compensation.")
            };
        }

        employee.Status = EmployeeStatus.Inactive;
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
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
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