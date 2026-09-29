using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Schedules;

public sealed class AvailabilityRule : ValueObject
{
    private AvailabilityRule(DayOfWeek dayOfWeek, TimeRange timeRange)
    {
        DayOfWeek = dayOfWeek;
        TimeRange = timeRange;
    }

    public DayOfWeek DayOfWeek { get; }
    public TimeRange TimeRange { get; }

    public static Result<AvailabilityRule> Create(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        var rangeResult = TimeRange.Create(start, end);
        if (rangeResult.IsFailure) return Result.Failure<AvailabilityRule>(rangeResult.Error);

        return Result.Success(new AvailabilityRule(dayOfWeek, rangeResult.Value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DayOfWeek;
        yield return TimeRange;
    }
}
