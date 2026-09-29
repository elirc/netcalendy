namespace NetCalendy.Domain.Schedules;

public record ScheduleId(Guid Value)
{
    public static ScheduleId New() => new(Guid.NewGuid());
    public static ScheduleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
