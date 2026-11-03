using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Contracts;
using Scheduler.Api.ErrorHandling;
using Scheduler.Application.Availability;

namespace Scheduler.Api.Controllers;

/// <summary>FR-06: open start times for a dealership, service type and local date.</summary>
[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController(GetAvailabilityHandler getAvailability, IMapper mapper) : ControllerBase
{
    /// <summary>Every 15-minute start time that can currently be booked on the given date.</summary>
    /// <param name="dealershipId">Dealership id.</param>
    /// <param name="serviceTypeId">Service type id.</param>
    /// <param name="date">Local date at the dealership, yyyy-MM-dd.</param>
    /// <param name="cancellationToken">Request abort token.</param>
    [HttpGet]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemResults.ProblemJsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemResults.ProblemJsonContentType)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? dealershipId,
        [FromQuery] Guid? serviceTypeId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await getAvailability.HandleAsync(
            new GetAvailabilityQuery(dealershipId, serviceTypeId, date), cancellationToken);

        return result.Error is { } error
            ? ProblemResults.ToActionResult(error, HttpContext)
            : Ok(mapper.Map<AvailabilityResponse>(result.Value));
    }
}
