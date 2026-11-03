using Scheduler.Domain.Entities;
using Scheduler.Domain.Results;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Services;

/// <summary>Time rules for a booking request: granularity (BR-07), horizon (BR-06), opening hours (BR-05).</summary>
public static class BookingPolicy
{
    public const int SlotMinutes = 15;

    public static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(60);

    public static readonly TimeSpan MaximumHorizon = TimeSpan.FromDays(60);

    /// <summary>
    /// BR-07: minutes on a 15-minute boundary and no seconds or fractions, evaluated on the clock time
    /// the client sent.
    /// </summary>
    public static bool IsAlignedToSlot(DateTimeOffset start) =>
        start.Minute % SlotMinutes == 0 && start.Ticks % TimeSpan.TicksPerMinute == 0;

    public static Result CheckSlotAlignment(DateTimeOffset start) =>
        IsAlignedToSlot(start) ? Result.Success() : DomainErrors.InvalidStartTime;

    /// <summary>BR-06: start at least 60 minutes and at most 60 days after "now" (both inclusive).</summary>
    public static BookingWindow GetBookingWindow(DateTimeOffset now) =>
        new(now + MinimumLeadTime, now + MaximumHorizon);

    public static Result CheckHorizon(DateTimeOffset start, DateTimeOffset now) =>
        GetBookingWindow(now).Contains(start) ? Result.Success() : DomainErrors.OutsideBookingHorizon;

    /// <summary>
    /// BR-05: the whole window lies within opening hours of one open day, in the dealership's time zone.
    /// Windows touching an ambiguous local hour (DST fall-back) are rejected.
    /// </summary>
    public static Result CheckOpeningHours(Dealership dealership, TimeSlot slot)
    {
        ArgumentNullException.ThrowIfNull(dealership);

        if (dealership.IsAmbiguousLocalTime(slot.StartUtc) || dealership.IsAmbiguousLocalTime(slot.EndUtc))
        {
            return DomainErrors.OutsideOpeningHours;
        }

        var localStart = dealership.ToLocal(slot.StartUtc).DateTime;
        var localEnd = dealership.ToLocal(slot.EndUtc).DateTime;

        var withinOneOpenDay = localStart.Date == localEnd.Date && dealership.IsOpenOn(localStart.DayOfWeek);
        var withinHours = TimeOnly.FromDateTime(localStart) >= dealership.OpeningTime
                          && TimeOnly.FromDateTime(localEnd) <= dealership.ClosingTime;

        return withinOneOpenDay && withinHours ? Result.Success() : DomainErrors.OutsideOpeningHours;
    }
}
