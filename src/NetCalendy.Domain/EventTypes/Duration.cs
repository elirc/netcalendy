using NetCalendy.Domain.Common;

namespace NetCalendy.Domain.EventTypes;

public sealed class Duration : ValueObject
{
    public static readonly int[] AllowedMinutes = [15, 30, 45, 60, 90, 120];

    private Duration(int minutes) => Minutes = minutes;

    public int Minutes { get; }

    public static Result<Duration> Create(int minutes)
    {
        if (minutes <= 0)
            return Result.Failure<Duration>(Error.Validation("Duration.NonPositive", "Duration must be positive."));

        if (minutes > 480)
            return Result.Failure<Duration>(Error.Validation("Duration.TooLong", "Duration cannot exceed 480 minutes."));

        return Result.Success(new Duration(minutes));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Minutes;
    }

    public override string ToString() => $"{Minutes}min";
}
