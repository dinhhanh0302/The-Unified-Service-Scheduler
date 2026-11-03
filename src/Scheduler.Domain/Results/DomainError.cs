namespace Scheduler.Domain.Results;

/// <summary>An expected business failure. <see cref="Code"/> matches the error catalog in SPEC §8.</summary>
public sealed record DomainError(string Code, string Message);
