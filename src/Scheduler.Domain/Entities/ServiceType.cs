using Scheduler.Domain.Enums;

namespace Scheduler.Domain.Entities;

/// <summary>What a job needs: how long it takes, which bay type and which technician qualification.</summary>
public sealed class ServiceType
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 480;
    public const int DurationStepMinutes = 15;

    public ServiceType(
        Guid id,
        string code,
        string name,
        int durationMinutes,
        Qualification requiredQualification,
        BayType requiredBayType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (durationMinutes is < MinDurationMinutes or > MaxDurationMinutes
            || durationMinutes % DurationStepMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                durationMinutes,
                $"Duration must be {MinDurationMinutes}–{MaxDurationMinutes} minutes and a multiple of {DurationStepMinutes}.");
        }

        Id = id;
        Code = code;
        Name = name;
        DurationMinutes = durationMinutes;
        RequiredQualification = requiredQualification;
        RequiredBayType = requiredBayType;
    }

    public Guid Id { get; }

    public string Code { get; }

    public string Name { get; }

    public int DurationMinutes { get; }

    public Qualification RequiredQualification { get; }

    public BayType RequiredBayType { get; }
}
