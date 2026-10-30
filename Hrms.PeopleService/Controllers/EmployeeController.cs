using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hrms.GrpcContracts.Compensation;
using Hrms.PeopleService.Interfaces;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.DTO;
using Hrms.PeopleService.Models.Enums;
using Hrms.PeopleService.Services;
using Hrms.PeopleService.Stores;
using Microsoft.AspNetCore.Http.HttpResults;
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
    private readonly IEmployeeLifecycleService _employeeLifecycleService;

    public EmployeeController(PeopleDbContext context,
        Channel<ImportJob> channel, ImportJobStore importJobStore,
        CompensationGrpc.CompensationGrpcClient compensationClient,
        IEmployeeLifecycleService employeeLifecycleService)
    {
        _context = context;
        _channel = channel;
        _jobStore = importJobStore;
        _compensationClient = compensationClient;
        _employeeLifecycleService = employeeLifecycleService;
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
            await _compensationClient.OpenCompensationAsync(
                new OpenCompensationRequest
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
    public async Task<IActionResult> Dismiss(int id, [FromBody] DismissEmployeeDto dismissEmployeeDto,
        CancellationToken cancellationToken)
    {
        var result = await _employeeLifecycleService.DismissAsync(
            id,
            dismissEmployeeDto.EffectiveTo,
            cancellationToken);

        return result.Status switch
        {
            EmployeeLifecycleStatus.Conflict => Conflict("Employee cannot be dismissed in the current state."),
            EmployeeLifecycleStatus.InvalidDate => BadRequest("Invalid dismissal date."),
            EmployeeLifecycleStatus.NotFound => NotFound(),
            EmployeeLifecycleStatus.DependencyUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Compensation service is temporarily unavailable."),
            EmployeeLifecycleStatus.DependencyTimeout => StatusCode(StatusCodes.Status504GatewayTimeout,
                "Compensation service did not respond in time."),
            EmployeeLifecycleStatus.Success => Ok(result.Employee),
            EmployeeLifecycleStatus.DependencyFailure => StatusCode(StatusCodes.Status500InternalServerError,
                "Internal server error."),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    [HttpGet("import/{jobId:guid}")]
    public IActionResult GetImportStatus(Guid jobId)
    {
        var job = _jobStore.Get(jobId);

        if (job is null)
            return NotFound();

        return Ok(job);
    }

    [HttpPost("{id:int}/rehire")]
    public async Task<IActionResult> Rehire(
        int id,
        [FromBody] RehireEmployeeDto request,
        CancellationToken cancellationToken)
    {
        var result = await _employeeLifecycleService.RehireAsync(
            id,
            request.BaseSalary,
            request.EffectiveFrom,
            cancellationToken);

        return result.Status switch
        {
            EmployeeLifecycleStatus.Success =>
                Ok(result.Employee),

            EmployeeLifecycleStatus.NotFound =>
                NotFound(),

            EmployeeLifecycleStatus.InvalidSalary =>
                BadRequest("BaseSalary is invalid."),

            EmployeeLifecycleStatus.InvalidDate =>
                BadRequest("EffectiveFrom must be UTC and cannot be in the future."),

            EmployeeLifecycleStatus.InvalidCompensationData =>
                BadRequest("Invalid compensation data."),

            EmployeeLifecycleStatus.Conflict =>
                Conflict("Employee cannot be rehired in the current state."),

            EmployeeLifecycleStatus.DependencyUnavailable =>
                StatusCode(503, "Compensation service is temporarily unavailable."),

            EmployeeLifecycleStatus.DependencyTimeout =>
                StatusCode(504, "Compensation service did not respond in time."),

            EmployeeLifecycleStatus.DependencyFailure =>
                StatusCode(500, "An error occurred while opening compensation."),

            _ => StatusCode(500, "Unexpected operation result.")
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        Employee? employee = _context.Employees.FirstOrDefault(employee => employee.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return Ok(employee);
    }
}