using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.CustomerId).HasColumnName("customer_id");
        builder.Property(a => a.VehicleId).HasColumnName("vehicle_id");
        builder.Property(a => a.DealershipId).HasColumnName("dealership_id");
        builder.Property(a => a.ServiceTypeId).HasColumnName("service_type_id");
        builder.Property(a => a.TechnicianId).HasColumnName("technician_id");
        builder.Property(a => a.ServiceBayId).HasColumnName("service_bay_id");
        builder.Property(a => a.StartUtc).HasColumnName("start_utc");
        builder.Property(a => a.EndUtc).HasColumnName("end_utc");

        // Stored as text so the exclusion constraints can filter on status = 'Confirmed'.
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.Notes).HasColumnName("notes").HasMaxLength(Appointment.MaxNotesLength);
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.CancelledAtUtc).HasColumnName("cancelled_at_utc");
        builder.Property(a => a.IdempotencyKey).HasColumnName("idempotency_key")
            .HasMaxLength(Appointment.MaxIdempotencyKeyLength);
        builder.Property(a => a.RequestHash).HasColumnName("request_hash").HasMaxLength(64);

        builder.Ignore(a => a.Slot);

        // Foreign keys (no navigations) so EF orders inserts correctly; the schema itself comes from SQL.
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Dealership>().WithMany().HasForeignKey(a => a.DealershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(a => a.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceBay>().WithMany().HasForeignKey(a => a.ServiceBayId).OnDelete(DeleteBehavior.Restrict);
    }
}
