using DataPipeline.Core.Interfaces;
using Hrms.Models;

namespace Hrms.Sinks;

public class EmployeeDbSink : IRecordSink<Employee>
{
    private readonly AppDbContext _dbContext;
    public EmployeeDbSink(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task WriteAsync(Employee record, CancellationToken cancellationToken = new CancellationToken())
    {
        await _dbContext.Employees.AddAsync(record, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}