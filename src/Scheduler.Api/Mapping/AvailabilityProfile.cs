using AutoMapper;
using Scheduler.Api.Contracts;
using Scheduler.Application.Availability;

namespace Scheduler.Api.Mapping;

public sealed class AvailabilityProfile : Profile
{
    public AvailabilityProfile()
    {
        CreateMap<AvailabilityDto, AvailabilityResponse>();
    }
}
