using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.Schedules;

public sealed class TimeRange : ValueObject
{
    private TimeRange(TimeOnly start, TimeOnly end)
    {
        Start = start;
        End = end;
    }

    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public static Result<TimeRange> Create(TimeOnly start, TimeOnly end)
    {
        if (start >= end)
            return Result.Failure<TimeRange>(Error.Validation("TimeRange.Invalid", "Start time must be before end time."));

        return Result.Success(new TimeRange(start, end));
    }

    public bool Overlaps(TimeRange other) =>
        Start < other.End && End > other.Start;

    public int TotalMinutes => (int)(End - Start).TotalMinutes;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
