using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Middleware;
using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Common;
using Scheduler.Domain.Results;

namespace Scheduler.Api.ErrorHandling;

/// <summary>
/// The single place that turns domain errors into RFC 9457 ProblemDetails, using the error catalog in
/// SPEC §8. Controllers never build error bodies by hand (SPEC §11).
/// </summary>
public static class ProblemResults
{
    public const string ProblemJsonContentType = "application/problem+json";
    public const string InternalErrorCode = "INTERNAL_ERROR";

    private static readonly Dictionary<string, int> StatusByCode = new(StringComparer.Ordinal)
    {
        [DomainErrors.ValidationFailed.Code] = StatusCodes.Status400BadRequest,
        [DomainErrors.InvalidStartTime.Code] = StatusCodes.Status400BadRequest,
        [DomainErrors.CustomerNotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.VehicleNotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.DealershipNotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.ServiceTypeNotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.AppointmentNotFound.Code] = StatusCodes.Status404NotFound,
        [DomainErrors.SlotUnavailable.Code] = StatusCodes.Status409Conflict,
        [DomainErrors.VehicleAlreadyBooked.Code] = StatusCodes.Status409Conflict,
        [DomainErrors.AppointmentNotCancellable.Code] = StatusCodes.Status409Conflict,
        [DomainErrors.OutsideOpeningHours.Code] = StatusCodes.Status422UnprocessableEntity,
        [DomainErrors.OutsideBookingHorizon.Code] = StatusCodes.Status422UnprocessableEntity,
        [DomainErrors.VehicleNotOwned.Code] = StatusCodes.Status422UnprocessableEntity,
        [DomainErrors.IdempotencyKeyReused.Code] = StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>HTTP status for an error code; unknown codes are a programming error and map to 500.</summary>
    public static int StatusFor(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;

    public static ProblemDetails ToProblemDetails(DomainError error, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = new ProblemDetails
        {
            Type = $"https://errors.scheduler/{error.Code.ToLowerInvariant().Replace('_', '-')}",
            Title = error.Message,
            Status = StatusFor(error.Code),
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions[CorrelationIdMiddleware.LogPropertyName] = httpContext.TraceIdentifier;

        switch (error)
        {
            case ValidationFailedError validation:
                problem.Extensions["errors"] = validation.Errors;
                break;
            case SlotUnavailableError unavailable:
                problem.Extensions["suggestedStartTimes"] = unavailable.SuggestedStartTimes;
                break;
        }

        return problem;
    }

    public static ObjectResult ToActionResult(DomainError error, HttpContext httpContext)
    {
        var problem = ToProblemDetails(error, httpContext);
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add(ProblemJsonContentType);
        return result;
    }
}
