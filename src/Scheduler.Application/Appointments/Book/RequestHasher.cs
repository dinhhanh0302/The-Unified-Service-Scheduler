using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Scheduler.Application.Appointments.Book;

/// <summary>
/// SHA-256 over a canonical form of the booking request (FR-08). The start time is hashed as a UTC instant,
/// so "09:00+07:00" and "02:00Z" count as the same request.
/// </summary>
public static class RequestHasher
{
    public static string Hash(ValidatedBooking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var canonical = string.Join(
            '|',
            booking.CustomerId.ToString("N"),
            booking.VehicleId.ToString("N"),
            booking.DealershipId.ToString("N"),
            booking.ServiceTypeId.ToString("N"),
            booking.Start.UtcTicks.ToString(CultureInfo.InvariantCulture),
            booking.Notes ?? string.Empty);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
