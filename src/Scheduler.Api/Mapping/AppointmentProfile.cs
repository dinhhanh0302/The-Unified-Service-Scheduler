using AutoMapper;
using Scheduler.Api.Contracts;
using Scheduler.Application.Appointments;

namespace Scheduler.Api.Mapping;

/// <summary>
/// Application DTO → API response (SPEC §11: Api layer only, never targets Domain entities).
/// Guarded by MappingConfigurationTests (AssertConfigurationIsValid).
/// </summary>
public sealed class AppointmentProfile : Profile
{
    public AppointmentProfile()
    {
        CreateMap<VehicleSummary, VehicleResponse>();
        CreateMap<ServiceTypeSummary, ServiceTypeResponse>();
        CreateMap<TechnicianSummary, TechnicianResponse>();
        CreateMap<ServiceBaySummary, ServiceBayResponse>();

        // Status enum → its name ("Confirmed" / "Cancelled").
        CreateMap<AppointmentDto, AppointmentResponse>();
    }
}
