using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping over the schema created by the SQL migrations. EF never creates or alters the
/// schema itself (SPEC §3): exclusion constraints and range types live in the SQL scripts.
/// </summary>
public sealed class SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : DbContext(options)
{
    public DbSet<Dealership> Dealerships => Set<Dealership>();

    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();

    public DbSet<Technician> Technicians => Set<Technician>();

    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);
}
