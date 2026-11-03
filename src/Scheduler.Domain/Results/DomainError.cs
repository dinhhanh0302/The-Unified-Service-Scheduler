namespace Scheduler.Domain.Results;

/// <summary>
/// An expected business failure. <see cref="Code"/> matches the error catalog in SPEC §8.
/// Not sealed: errors that carry extra data (field errors, suggested times) derive from it.
/// </summary>
public record DomainError(string Code, string Message);
