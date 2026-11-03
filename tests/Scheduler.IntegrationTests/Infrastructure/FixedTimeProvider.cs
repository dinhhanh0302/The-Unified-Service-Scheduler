namespace Scheduler.IntegrationTests.Infrastructure;

/// <summary>Freezes "now" for the API under test (SPEC §7: Mon 2026-11-09 08:00 +07:00).</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
