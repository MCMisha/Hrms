using Hrms.PeopleService.Models.Enums;

namespace Hrms.PeopleService.Models;

public record ImportJobState(
    Guid Id,
    ImportJobStatus Status,
    string? Error = null);