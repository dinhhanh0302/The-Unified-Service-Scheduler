using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Scheduler.Api.Observability;

public static class ObservabilityExtensions
{
    // Npgsql emits traces and metrics natively under this name; no extra package needed.
    private const string NpgsqlInstrumentationName = "Npgsql";

    public static IServiceCollection AddSchedulerTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        // OTLP is enabled only when an endpoint is configured (standard OTEL_EXPORTER_OTLP_ENDPOINT).
        var otlpEnabled = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var consoleEnabled = configuration.GetValue<bool>("Telemetry:ConsoleExporter");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(SchedulerTelemetry.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddSource(NpgsqlInstrumentationName)
                    .AddSource(SchedulerTelemetry.ActivitySourceName);

                if (otlpEnabled) tracing.AddOtlpExporter();
                if (consoleEnabled) tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(NpgsqlInstrumentationName)
                    .AddMeter(SchedulerTelemetry.MeterName);

                if (otlpEnabled) metrics.AddOtlpExporter();
                if (consoleEnabled) metrics.AddConsoleExporter();
            });

        return services;
    }
}
