using Scheduler.Domain.Enums;

namespace Scheduler.Domain.Entities;

/// <summary>A physical work position (lift, diagnostic station...). Inactive bays are never allocated (BR-02).</summary>
public sealed class ServiceBay
{
    public ServiceBay(Guid id, Guid dealershipId, string name, BayType bayType, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        DealershipId = dealershipId;
        Name = name;
        BayType = bayType;
        IsActive = isActive;
    }

    public Guid Id { get; }

    public Guid DealershipId { get; }

    /// <summary>Unique within a dealership; used for deterministic allocation (BR-10).</summary>
    public string Name { get; }

    public BayType BayType { get; }

    public bool IsActive { get; }
}
