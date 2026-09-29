namespace NetCalendy.Domain.Schedules;

public record TimeSlot(DateOnly Date, TimeOnly Start, TimeOnly End)
{
    public DateTimeOffset ToUtc(TimeZoneInfo tz)
    {
        var local = new DateTime(Date.Year, Date.Month, Date.Day, Start.Hour, Start.Minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }
}
