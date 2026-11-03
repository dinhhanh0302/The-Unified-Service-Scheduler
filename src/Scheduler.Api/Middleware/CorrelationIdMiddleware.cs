using System.Diagnostics;
using Serilog.Context;

namespace Scheduler.Api.Middleware;

/// <summary>
/// Reads X-Correlation-Id (or generates one), exposes it as HttpContext.TraceIdentifier,
/// echoes it on the response and attaches it to every log entry and the current trace (SPEC §10).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogPropertyName = "correlationId";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName].ToString());

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(LogPropertyName, correlationId);

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    /// <summary>Accepts a caller-supplied id only if it is short and made of safe characters.</summary>
    internal static string ResolveCorrelationId(string? incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming)
            && incoming.Length <= MaxLength
            && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return incoming;
        }

        return Guid.NewGuid().ToString("N");
    }
}
