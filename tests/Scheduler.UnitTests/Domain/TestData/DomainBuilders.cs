using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Domain.Services;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.UnitTests.Domain.TestData;

/// <summary>Seed-like data from SPEC §7: dealership D1 and service type OIL_CHANGE.</summary>
internal static class DomainBuilders
{
    public static readonly Guid D1Id = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    public static Dealership D1() => new(
        D1Id,
        "D1",
        "Asia/Ho_Chi_Minh",
        new TimeOnly(8, 0),
        new TimeOnly(17, 0),
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]);

    public static ServiceType OilChange() => new(
        Guid.Parse("00000000-0000-0000-0000-00000000011c"),
        "OIL_CHANGE",
        "Oil change",
        60,
        Qualification.General,
        BayType.Lift);

    public static ServiceBay Bay(
        string name,
        BayType bayType = BayType.Lift,
        bool isActive = true,
        Guid? id = null,
        Guid? dealershipId = null) =>
        new(id ?? Guid.NewGuid(), dealershipId ?? D1Id, name, bayType, isActive);

    public static Technician Tech(
        Guid? id = null,
        Qualification[]? qualifications = null,
        bool isActive = true,
        Guid? dealershipId = null) =>
        new(id ?? Guid.NewGuid(), dealershipId ?? D1Id, "Technician", qualifications ?? [Qualification.General], isActive);

    /// <summary>A confirmed booking that occupies only the given bay (technician is someone else).</summary>
    public static BookedSlot BayBusy(ServiceBay bay, DateTimeOffset start, DateTimeOffset end) =>
        new(bay.Id, Guid.NewGuid(), new TimeSlot(start, end));

    /// <summary>A confirmed booking that occupies only the given technician (bay is elsewhere).</summary>
    public static BookedSlot TechBusy(Technician technician, DateTimeOffset start, DateTimeOffset end) =>
        new(Guid.NewGuid(), technician.Id, new TimeSlot(start, end));

    public static BookedSlot BothBusy(ServiceBay bay, Technician technician, DateTimeOffset start, DateTimeOffset end) =>
        new(bay.Id, technician.Id, new TimeSlot(start, end));

    public static ResourceSnapshot Snapshot(
        IEnumerable<ServiceBay> bays,
        IEnumerable<Technician> technicians,
        params BookedSlot[] bookings) =>
        new(bays, technicians, bookings);
}
