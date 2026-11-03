namespace Scheduler.UnitTests.Domain.TestData;

/// <summary>Fixed test context from SPEC §7: now = Mon 2026-11-09 08:00 +07:00, "Tue" = 2026-11-10.</summary>
internal static class TestClock
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, VietnamOffset);

    public static readonly DateOnly Monday = new(2026, 11, 9);

    public static readonly DateOnly Tuesday = new(2026, 11, 10);

    public static readonly DateOnly Sunday = new(2026, 11, 15);

    public static DateTimeOffset Mon(int hour, int minute = 0) => new(2026, 11, 9, hour, minute, 0, VietnamOffset);

    public static DateTimeOffset Tue(int hour, int minute = 0) => new(2026, 11, 10, hour, minute, 0, VietnamOffset);

    public static DateTimeOffset Sun(int hour, int minute = 0) => new(2026, 11, 15, hour, minute, 0, VietnamOffset);
}
