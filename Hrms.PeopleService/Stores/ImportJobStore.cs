using System.Collections.Concurrent;
using Hrms.PeopleService.Models;
using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Stores;

public class ImportJobStore
{
    private readonly ConcurrentDictionary<Guid, ImportJobState> _jobs = [];

    public void Add(Guid id)
    {
        _jobs[id] = new ImportJobState(
            id,
            ImportJobStatus.Pending);
    }

    public void SetStatus(
        Guid id,
        ImportJobStatus status,
        string? error = null)
    {
        _jobs[id] = new ImportJobState(
            id,
            status,
            error);
    }

    public ImportJobState? Get(Guid id)
    {
        return _jobs.GetValueOrDefault(id);
    }
}