using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Services;

/// <summary>
/// Pure, in-memory availability logic (no I/O). Decides which bay and technician can take a window,
/// lists free start times for a day and suggests alternatives. The database exclusion constraints remain
/// the final guarantee under concurrency (BR-11).
/// </summary>
public static class AvailabilityEngine
{
    public const int MaxSuggestions = 3;

    /// <summary>
    /// BR-02 – BR-04 and BR-10: a compatible, active, free bay AND a qualified, active, free technician for
    /// the entire window, or null. Bay: lowest name (ordinal), then lowest id. Technician: fewest booked
    /// minutes that day, then lowest id.
    /// </summary>
    public static Allocation? Allocate(
        Dealership dealership,
        ServiceType serviceType,
        TimeSlot slot,
        ResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(dealership);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(snapshot);

        var bay = snapshot.Bays
            .Where(b => b.DealershipId == dealership.Id
                        && b.IsActive
                        && b.BayType == serviceType.RequiredBayType
                        && !snapshot.IsBayBusy(b.Id, slot))
            .OrderBy(b => b.Name, StringComparer.Ordinal)
            .ThenBy(b => b.Id)
            .FirstOrDefault();

        if (bay is null)
        {
            return null;
        }

        var technician = snapshot.Technicians
            .Where(t => t.DealershipId == dealership.Id
                        && t.IsActive
                        && t.HasQualification(serviceType.RequiredQualification)
                        && !snapshot.IsTechnicianBusy(t.Id, slot))
            .OrderBy(t => snapshot.BookedMinutes(t.Id))
            .ThenBy(t => t.Id)
            .FirstOrDefault();

        return technician is null ? null : new Allocation(bay, technician);
    }

    /// <summary>
    /// FR-06: every 15-minute start on <paramref name="localDate"/> that fits opening hours, lies inside the
    /// booking window and has a bay and technician free. Returned as UTC instants, in order.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> GetAvailableStartTimes(
        Dealership dealership,
        ServiceType serviceType,
        DateOnly localDate,
        ResourceSnapshot snapshot,
        BookingWindow window)
    {
        ArgumentNullException.ThrowIfNull(dealership);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!dealership.IsOpenOn(localDate.DayOfWeek))
        {
            return [];
        }

        var openingMinute = (int)dealership.OpeningTime.ToTimeSpan().TotalMinutes;
        var closingMinute = (int)dealership.ClosingTime.ToTimeSpan().TotalMinutes;
        var firstMinute = RoundUpToSlot(openingMinute);

        var startTimes = new List<DateTimeOffset>();
        for (var minute = firstMinute; minute + serviceType.DurationMinutes <= closingMinute; minute += BookingPolicy.SlotMinutes)
        {
            var startUtc = dealership.ToUtc(localDate, TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute)));
            if (startUtc is null || !window.Contains(startUtc.Value))
            {
                continue;
            }

            var slot = TimeSlot.FromDuration(startUtc.Value, serviceType.DurationMinutes);
            if (BookingPolicy.CheckOpeningHours(dealership, slot).IsFailure)
            {
                continue;
            }

            if (Allocate(dealership, serviceType, slot, snapshot) is not null)
            {
                startTimes.Add(startUtc.Value);
            }
        }

        return startTimes;
    }

    /// <summary>
    /// FR-05: up to three start times later on the same local day that are free for a bay, a technician
    /// AND the requested vehicle (<paramref name="vehicleBookings"/> are its confirmed windows).
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> SuggestStartTimes(
        Dealership dealership,
        ServiceType serviceType,
        TimeSlot requested,
        ResourceSnapshot snapshot,
        IReadOnlyCollection<TimeSlot> vehicleBookings,
        BookingWindow window)
    {
        ArgumentNullException.ThrowIfNull(dealership);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(vehicleBookings);

        var localDate = dealership.LocalDateOf(requested.StartUtc);

        return GetAvailableStartTimes(dealership, serviceType, localDate, snapshot, window)
            .Where(start => start > requested.StartUtc)
            .Where(start =>
            {
                var candidate = TimeSlot.FromDuration(start, serviceType.DurationMinutes);
                return !vehicleBookings.Any(booked => booked.Overlaps(candidate));
            })
            .Take(MaxSuggestions)
            .ToArray();
    }

    private static int RoundUpToSlot(int minute) =>
        (minute + BookingPolicy.SlotMinutes - 1) / BookingPolicy.SlotMinutes * BookingPolicy.SlotMinutes;
}
