using AutoMapper;
using Scheduler.Api.Contracts;
using Scheduler.Application.ReferenceData;

namespace Scheduler.Api.Mapping;

/// <summary>Reference data DTOs → API responses; enums become their names.</summary>
public sealed class ReferenceDataProfile : Profile
{
    public ReferenceDataProfile()
    {
        CreateMap<DealershipDto, DealershipResponse>();
        CreateMap<ServiceTypeDto, ServiceTypeDetailsResponse>();
        CreateMap<VehicleDto, CustomerVehicleResponse>();
    }
}
