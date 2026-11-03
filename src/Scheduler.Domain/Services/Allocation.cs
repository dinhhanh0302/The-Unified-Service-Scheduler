using Scheduler.Domain.Entities;

namespace Scheduler.Domain.Services;

/// <summary>The bay and technician chosen for a booking window.</summary>
public sealed record Allocation(ServiceBay Bay, Technician Technician);
