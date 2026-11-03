using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("service_types");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(s => s.RequiredQualification).HasColumnName("required_qualification");
        builder.Property(s => s.RequiredBayType).HasColumnName("required_bay_type");
    }
}
