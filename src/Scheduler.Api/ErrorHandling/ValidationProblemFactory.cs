using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Common;

namespace Scheduler.Api.ErrorHandling;

/// <summary>
/// Replaces [ApiController]'s automatic 400 (malformed JSON, wrong types such as an invalid GUID, empty body)
/// with the same VALIDATION_FAILED shape the validator produces (SPEC §11).
/// </summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => NormalizeKey(entry.Key),
                entry => entry.Value!.Errors // Null-forgiving is safe: filtered on Value is not null above.
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The value is invalid." : e.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        return ProblemResults.ToActionResult(new ValidationFailedError(errors), context.HttpContext);
    }

    /// <summary>"$.customerId" → "customerId"; an empty key (whole body) → "body".</summary>
    private static string NormalizeKey(string key)
    {
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        return string.IsNullOrEmpty(trimmed) ? "body" : trimmed;
    }
}
