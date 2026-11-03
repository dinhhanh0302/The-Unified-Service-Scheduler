using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scheduler.Api.ErrorHandling;
using Scheduler.Api.Mapping;
using Scheduler.Api.Middleware;
using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Appointments.Cancel;
using Scheduler.Application.Appointments.Get;
using Scheduler.Application.Availability;
using Scheduler.Application.ReferenceData;
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
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);

    // Framework-generated problems (unhandled exceptions, 404 without a body...) also carry the correlation id.
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions[CorrelationIdMiddleware.LogPropertyName] =
                context.HttpContext.TraceIdentifier;
            if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                context.ProblemDetails.Extensions["code"] = ProblemResults.InternalErrorCode;
            }
        });
    builder.Services.AddOpenApi();

    // Object mapping (SPEC §3, OQ-15): Api layer only.
    builder.Services.AddAutoMapper(_ => { }, typeof(AppointmentProfile));

    // Use cases (Application is BCL-only, so they are registered here).
    builder.Services.AddScoped<BookAppointmentHandler>();
    builder.Services.AddScoped<GetAppointmentHandler>();
    builder.Services.AddScoped<CancelAppointmentHandler>();
    builder.Services.AddScoped<GetAvailabilityHandler>();
    builder.Services.AddScoped<ReferenceDataQueries>();

    // Infrastructure
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
