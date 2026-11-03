using Scheduler.Domain.Enums;

namespace Scheduler.Domain.Entities;

/// <summary>A technician of one dealership. Inactive technicians are never allocated (BR-03).</summary>
public sealed class Technician
{
    public Technician(
        Guid id,
        Guid dealershipId,
        string fullName,
        IEnumerable<Qualification> qualifications,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(qualifications);

        Id = id;
        DealershipId = dealershipId;
        FullName = fullName;
        Qualifications = qualifications.Distinct().Order().ToArray();
        IsActive = isActive;
    }

    public Guid Id { get; }

    public Guid DealershipId { get; }

    public string FullName { get; }

    public IReadOnlyList<Qualification> Qualifications { get; }

    public bool IsActive { get; }

    public bool HasQualification(Qualification qualification) => Qualifications.Contains(qualification);
}
