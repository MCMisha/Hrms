using DataPipeline.Core.Interfaces;
using Hrms.PeopleService.Models;

namespace Hrms.PeopleService.Sinks;

public class EmployeeDbSink : IRecordSink<Employee>
{
    private readonly PeopleDbContext _dbContext;
    public EmployeeDbSink(PeopleDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task WriteAsync(Employee record, CancellationToken cancellationToken = new CancellationToken())
    {
        await _dbContext.Employees.AddAsync(record, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}