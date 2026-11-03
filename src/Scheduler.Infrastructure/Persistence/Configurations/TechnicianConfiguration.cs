using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("technicians");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.DealershipId).HasColumnName("dealership_id");
        builder.Property(t => t.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Qualifications)
            .HasColumnName("qualifications")
            .HasConversion(EnumArrayConversions.QualificationsToInts, EnumArrayConversions.QualificationsComparer)
            .IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active");

        builder.HasOne<Dealership>().WithMany().HasForeignKey(t => t.DealershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
