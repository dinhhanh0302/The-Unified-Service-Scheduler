namespace Scheduler.Api.Observability;

/// <summary>
/// Names of the custom ActivitySource and Meter (SPEC §10). Instruments are added in later steps.
/// </summary>
public static class SchedulerTelemetry
{
    public const string ServiceName = "scheduler-api";
    public const string ActivitySourceName = "Scheduler";
    public const string MeterName = "Scheduler";
}
