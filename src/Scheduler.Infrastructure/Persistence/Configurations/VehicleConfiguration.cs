using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.Vin).HasColumnName("vin").HasMaxLength(Vehicle.VinLength).IsRequired();
        builder.Property(v => v.Make).HasColumnName("make").HasMaxLength(100).IsRequired();
        builder.Property(v => v.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(v => v.Year).HasColumnName("year");
        builder.Property(v => v.CustomerId).HasColumnName("customer_id");

        builder.HasOne<Customer>().WithMany().HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
