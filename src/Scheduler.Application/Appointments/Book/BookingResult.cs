namespace Scheduler.Application.Appointments.Book;

/// <summary>A confirmed booking; <see cref="Replayed"/> is true when an idempotent retry returned the original.</summary>
public sealed record BookingResult(AppointmentDto Appointment, bool Replayed);
