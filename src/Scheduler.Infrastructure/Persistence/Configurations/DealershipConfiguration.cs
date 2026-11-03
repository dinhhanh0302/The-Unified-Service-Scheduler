using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("dealerships");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).IsRequired();
        builder.Property(d => d.OpeningTime).HasColumnName("opening_time");
        builder.Property(d => d.ClosingTime).HasColumnName("closing_time");
        builder.Property(d => d.OpenDays)
            .HasColumnName("open_days")
            .HasConversion(EnumArrayConversions.DaysToInts, EnumArrayConversions.DaysComparer)
            .IsRequired();
    }
}
