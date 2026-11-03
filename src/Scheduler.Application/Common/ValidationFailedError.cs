using Scheduler.Domain.Results;

namespace Scheduler.Application.Common;

/// <summary>VALIDATION_FAILED with per-field messages (AC-01.20). Keys use the API's field names.</summary>
public sealed record ValidationFailedError(IReadOnlyDictionary<string, string[]> Errors)
    : DomainError(DomainErrors.ValidationFailed.Code, DomainErrors.ValidationFailed.Message);
