using DataPipeline.Core.Interfaces;
using DataPipeline.Core.Models;

namespace Hrms.Sinks;

public class EmployeeErrorSink : IErrorSink
{
    public Task WriteAsync(IReadOnlyList<MappingError> errors, CancellationToken token = new CancellationToken())
    {
        Console.WriteLine(errors[0]);
        Console.WriteLine(errors.Count);
        return Task.CompletedTask;
    }
}