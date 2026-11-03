namespace Scheduler.Infrastructure.Health;

public static class HealthTags
{
    /// <summary>Process is up. Used by /health/live; never touches dependencies.</summary>
    public const string Live = "live";

    /// <summary>Process can serve traffic. Used by /health/ready; includes the database.</summary>
    public const string Ready = "ready";
}
