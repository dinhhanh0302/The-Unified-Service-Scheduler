using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scheduler.Domain.Enums;

namespace Scheduler.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps read-only enum lists on the domain entities to PostgreSQL <c>integer[]</c> columns
/// (SDD §4: simple arrays instead of join tables at this scale).
/// </summary>
internal static class EnumArrayConversions
{
    public static readonly ValueConverter<IReadOnlyList<DayOfWeek>, int[]> DaysToInts = new(
        days => days.Select(day => (int)day).ToArray(),
        values => values.Select(value => (DayOfWeek)value).ToArray());

    public static readonly ValueComparer<IReadOnlyList<DayOfWeek>> DaysComparer = new(
        (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
        days => days.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
        days => days.ToArray());

    public static readonly ValueConverter<IReadOnlyList<Qualification>, int[]> QualificationsToInts = new(
        qualifications => qualifications.Select(q => (int)q).ToArray(),
        values => values.Select(value => (Qualification)value).ToArray());

    public static readonly ValueComparer<IReadOnlyList<Qualification>> QualificationsComparer = new(
        (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
        qualifications => qualifications.Aggregate(0, (hash, q) => HashCode.Combine(hash, q)),
        qualifications => qualifications.ToArray());
}
