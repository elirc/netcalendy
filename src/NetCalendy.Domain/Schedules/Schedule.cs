using NetCalendy.Domain.Common;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Schedules;

public sealed class Schedule : AggregateRoot<ScheduleId>
{
    private readonly List<AvailabilityRule> _rules = [];

    private Schedule(ScheduleId id, TenantId tenantId, UserId userId, string name, string timezone)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Timezone = timezone;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public TenantId TenantId { get; }
    public UserId UserId { get; }
    public string Name { get; private set; }
    public string Timezone { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<AvailabilityRule> Rules => _rules.AsReadOnly();

    public static Result<Schedule> Create(TenantId tenantId, UserId userId, string name, string timezone)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Schedule>(Error.Validation("Schedule.InvalidName", "Name cannot be empty."));

        if (string.IsNullOrWhiteSpace(timezone))
            return Result.Failure<Schedule>(Error.Validation("Schedule.InvalidTimezone", "Timezone cannot be empty."));

        var schedule = new Schedule(ScheduleId.New(), tenantId, userId, name.Trim(), timezone);
        schedule.AddDefaultWorkweekRules();
        return Result.Success(schedule);
    }

    public Result SetRules(IEnumerable<(DayOfWeek day, TimeOnly start, TimeOnly end)> rules)
    {
        _rules.Clear();
        foreach (var (day, start, end) in rules)
        {
            var result = AvailabilityRule.Create(day, start, end);
            if (result.IsFailure) return Result.Failure(result.Error);
            _rules.Add(result.Value);
        }
        return Result.Success();
    }

    public IEnumerable<TimeSlot> GetAvailableSlots(DateOnly date, int durationMinutes)
    {
        var dayRule = _rules.FirstOrDefault(r => r.DayOfWeek == date.DayOfWeek);
        if (dayRule is null) yield break;

        var current = dayRule.TimeRange.Start;
        var end = dayRule.TimeRange.End;

        while (current.AddMinutes(durationMinutes) <= end)
        {
            yield return new TimeSlot(date, current, current.AddMinutes(durationMinutes));
            current = current.AddMinutes(durationMinutes);
        }
    }

    private void AddDefaultWorkweekRules()
    {
        var workdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        foreach (var day in workdays)
        {
            var rule = AvailabilityRule.Create(day, start, end);
            if (rule.IsSuccess) _rules.Add(rule.Value);
        }
    }
}
