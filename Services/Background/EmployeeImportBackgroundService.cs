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

namespace Hrms.Services.Background;

public class EmployeeImportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<ImportJob> _channel;
    private readonly ImportJobStore _store;
    
    public EmployeeImportBackgroundService(
        IServiceScopeFactory scopeFactory,
        Channel<ImportJob> channel,
        ImportJobStore jobStore)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _store = jobStore;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _store.SetStatus(
                job.Id,
                ImportJobStatus.Processing);
            await ProcessJobAsync(job, stoppingToken);
            
        }
    }

    private async Task ProcessJobAsync(
        ImportJob job,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var processingPipeline = new PipelineBuilder<Employee>()
                .Transform(employee => employee with
                {
                    FirstName = Capitalize(employee.FirstName.Trim()),
                    LastName = Capitalize(employee.LastName.Trim()),
                    Email = employee.Email.Trim().ToLowerInvariant(),
                    Position = employee.Position.Trim()
                })
                .If(employee =>
                    employee.Status == EmployeeStatus.Active &&
                    employee.DateOfBirth.Date <= DateTime.Today.AddYears(-65))
                .Then(employee => employee with
                {
                    Status = EmployeeStatus.Inactive
                })
                .If(employee =>
                    employee.Status == EmployeeStatus.Active &&
                    employee.Salary < 2000m)
                .Then(employee => employee with
                {
                    Salary = 2000m
                })
                .Build();

            var employeeMapper = new EmployeeMapper();
            var recordSink = new EmployeeDbSink(context);
            var errorSink = new EmployeeErrorSink();
            var dataSource = new CsvDataSource<Employee>();

            IPipelineRunner<Employee> pipelineRunner =
                new PipelineRunner<Employee>(
                    dataSource,
                    employeeMapper,
                    recordSink,
                    errorSink,
                    processingPipeline);

            await using var stream = File.OpenRead(job.FilePath);

            await pipelineRunner.RunAsync(
                stream,
                cancellationToken);
            _store.SetStatus(job.Id, ImportJobStatus.Completed);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _store.SetStatus(
                job.Id,
                ImportJobStatus.Failed,
                ex.Message);
        }
        finally
        {
            if (File.Exists(job.FilePath))
            {
                File.Delete(job.FilePath);
            }
        }
    }

    private static string Capitalize(string input)
    {
        return char.ToUpperInvariant(input[0])
               + input[1..].ToLowerInvariant();
    }
}