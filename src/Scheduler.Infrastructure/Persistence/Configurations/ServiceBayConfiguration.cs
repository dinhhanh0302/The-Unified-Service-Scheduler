using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        builder.ToTable("service_bays");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.DealershipId).HasColumnName("dealership_id");
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(b => b.BayType).HasColumnName("bay_type");
        builder.Property(b => b.IsActive).HasColumnName("is_active");

        builder.HasOne<Dealership>().WithMany().HasForeignKey(b => b.DealershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
