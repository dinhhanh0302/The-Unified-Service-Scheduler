using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Services;

/// <summary>A confirmed booking that occupies a bay and a technician.</summary>
public sealed record BookedSlot(Guid ServiceBayId, Guid TechnicianId, TimeSlot Slot);

/// <summary>
/// Everything the engine needs for one dealership and one local day: its bays, its technicians and the
/// <c>Confirmed</c> appointments of that day. Loaded once per booking attempt (SPEC §10, performance).
/// </summary>
public sealed class ResourceSnapshot
{
    public ResourceSnapshot(
        IEnumerable<ServiceBay> bays,
        IEnumerable<Technician> technicians,
        IEnumerable<BookedSlot> bookings)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(technicians);
        ArgumentNullException.ThrowIfNull(bookings);

        Bays = bays.ToArray();
        Technicians = technicians.ToArray();
        Bookings = bookings.ToArray();
    }

    public IReadOnlyList<ServiceBay> Bays { get; }

    public IReadOnlyList<Technician> Technicians { get; }

    public IReadOnlyList<BookedSlot> Bookings { get; }

    public bool IsBayBusy(Guid bayId, TimeSlot slot) =>
        Bookings.Any(b => b.ServiceBayId == bayId && b.Slot.Overlaps(slot));

    public bool IsTechnicianBusy(Guid technicianId, TimeSlot slot) =>
        Bookings.Any(b => b.TechnicianId == technicianId && b.Slot.Overlaps(slot));

    /// <summary>Total booked minutes of a technician in this snapshot's day (BR-10, A-12).</summary>
    public double BookedMinutes(Guid technicianId) =>
        Bookings.Where(b => b.TechnicianId == technicianId).Sum(b => b.Slot.Duration.TotalMinutes);
}
