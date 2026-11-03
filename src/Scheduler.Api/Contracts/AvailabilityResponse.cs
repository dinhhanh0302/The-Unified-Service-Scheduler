namespace Scheduler.Api.Contracts;

/// <summary>GET /api/v1/availability response (SPEC §8): { "date": "2026-11-10", "startTimes": [...] }.</summary>
public sealed record AvailabilityResponse(DateOnly Date, IReadOnlyList<DateTimeOffset> StartTimes);
