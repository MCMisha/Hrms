using Hrms.Models.Enums;

namespace Hrms.Models;

public record ImportJobState(
    Guid Id,
    ImportJobStatus Status,
    string? Error = null);