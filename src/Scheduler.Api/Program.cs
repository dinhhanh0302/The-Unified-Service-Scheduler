using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scheduler.Api.Middleware;
using Scheduler.Api.Observability;
using Scheduler.Infrastructure;
using Scheduler.Infrastructure.Health;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logging: Serilog JSON to console (SPEC §3, §10)
    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    // API: Controllers only (SPEC §3, §11)
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions[CorrelationIdMiddleware.LogPropertyName] =
                context.HttpContext.TraceIdentifier);
    builder.Services.AddOpenApi();

    // Observability and infrastructure
    builder.Services.AddSchedulerTelemetry(builder.Configuration);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddInfrastructure();
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [HealthTags.Live]);

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Client stub (SPEC §9): OpenAPI document at /openapi/v1.json, Swagger UI at /swagger
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Unified Service Scheduler API v1");
        options.RoutePrefix = "swagger";
    });

    app.MapControllers();

    // Health endpoints are the only non-controller routes (SPEC §3)
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(HealthTags.Live)
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(HealthTags.Ready)
    });

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Scheduler API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
