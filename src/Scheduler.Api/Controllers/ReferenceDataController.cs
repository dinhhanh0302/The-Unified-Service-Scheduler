using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Api.Contracts;
using Scheduler.Api.ErrorHandling;
using Scheduler.Application.ReferenceData;

namespace Scheduler.Api.Controllers;

/// <summary>FR-09: reference data for building a booking request (read-only; managed by seed data).</summary>
[ApiController]
[Route("api/v1")]
public sealed class ReferenceDataController(ReferenceDataQueries queries, IMapper mapper) : ControllerBase
{
    /// <summary>All dealerships with their opening hours and time zone.</summary>
    [HttpGet("dealerships")]
    [ProducesResponseType<IReadOnlyList<DealershipResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDealerships(CancellationToken cancellationToken) =>
        Ok(mapper.Map<IReadOnlyList<DealershipResponse>>(await queries.ListDealershipsAsync(cancellationToken)));

    /// <summary>All service types with duration and resource requirements.</summary>
    [HttpGet("service-types")]
    [ProducesResponseType<IReadOnlyList<ServiceTypeDetailsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServiceTypes(CancellationToken cancellationToken) =>
        Ok(mapper.Map<IReadOnlyList<ServiceTypeDetailsResponse>>(await queries.ListServiceTypesAsync(cancellationToken)));

    /// <summary>The vehicles owned by a customer.</summary>
    [HttpGet("customers/{customerId:guid}/vehicles")]
    [ProducesResponseType<IReadOnlyList<CustomerVehicleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemResults.ProblemJsonContentType)]
    public async Task<IActionResult> ListCustomerVehicles(Guid customerId, CancellationToken cancellationToken)
    {
        var result = await queries.ListCustomerVehiclesAsync(customerId, cancellationToken);

        return result.Error is { } error
            ? ProblemResults.ToActionResult(error, HttpContext)
            : Ok(mapper.Map<IReadOnlyList<CustomerVehicleResponse>>(result.Value));
    }
}
