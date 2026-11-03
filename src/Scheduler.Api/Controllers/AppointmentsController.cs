using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Contracts;
using Scheduler.Api.ErrorHandling;
using Scheduler.Application.Appointments.Book;
using Scheduler.Application.Appointments.Cancel;
using Scheduler.Application.Appointments.Get;

namespace Scheduler.Api.Controllers;

/// <summary>Booking and reading appointments (FR-01 – FR-05, FR-08). Thin: request → use case → response.</summary>
[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController(
    BookAppointmentHandler bookAppointment,
    GetAppointmentHandler getAppointment,
    CancelAppointmentHandler cancelAppointment,
    IMapper mapper) : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string IdempotencyReplayedHeader = "Idempotency-Replayed";

    /// <summary>Request a service appointment; the bay and technician are assigned automatically.</summary>
    [HttpPost]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemResults.ProblemJsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemResults.ProblemJsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemResults.ProblemJsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemResults.ProblemJsonContentType)]
    public async Task<IActionResult> Book(
        [FromBody] BookAppointmentRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await bookAppointment.HandleAsync(request.ToCommand(idempotencyKey), cancellationToken);
        if (result.Error is { } error)
        {
            return ProblemResults.ToActionResult(error, HttpContext);
        }

        if (result.Value.Replayed)
        {
            Response.Headers[IdempotencyReplayedHeader] = "true";
        }

        var response = mapper.Map<AppointmentResponse>(result.Value.Appointment);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>Retrieve an appointment by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemResults.ProblemJsonContentType)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getAppointment.HandleAsync(id, cancellationToken);

        return result.Error is { } error
            ? ProblemResults.ToActionResult(error, HttpContext)
            : Ok(mapper.Map<AppointmentResponse>(result.Value));
    }

    /// <summary>Cancel a confirmed appointment that has not started; its bay and technician become free.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemResults.ProblemJsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemResults.ProblemJsonContentType)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await cancelAppointment.HandleAsync(id, cancellationToken);

        return result.Error is { } error
            ? ProblemResults.ToActionResult(error, HttpContext)
            : Ok(mapper.Map<AppointmentResponse>(result.Value));
    }
}
