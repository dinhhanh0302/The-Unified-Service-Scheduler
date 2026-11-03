using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(50).IsRequired();
    }
}
