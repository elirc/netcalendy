using FluentAssertions;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using Xunit;

namespace NetCalendy.Domain.Tests.ScheduleTests;

public sealed class ScheduleAggregateTests
{
    private static readonly TenantId TenantId = TenantId.New();
    private static readonly UserId UserId = UserId.New();

    [Fact]
    public void Create_WithValidData_ShouldAddDefaultWorkweekRules()
    {
        var result = Schedule.Create(TenantId, UserId, "Working Hours", "UTC");

        result.IsSuccess.Should().BeTrue();
        result.Value.Rules.Should().HaveCount(5);
        result.Value.Rules.Select(r => r.DayOfWeek).Should().BeEquivalentTo(new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
        });
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        var result = Schedule.Create(TenantId, UserId, "", "UTC");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Schedule.InvalidName");
    }

    [Fact]
    public void GetAvailableSlots_OnWorkday_ShouldReturnSlots()
    {
        var schedule = Schedule.Create(TenantId, UserId, "Working Hours", "UTC").Value;
        var monday = GetNextMonday();

        var slots = schedule.GetAvailableSlots(monday, 30).ToList();

        slots.Should().NotBeEmpty();
        slots.Should().HaveCount(16); // 9:00-17:00 in 30-min intervals
        slots.First().Start.Should().Be(new TimeOnly(9, 0));
        slots.Last().End.Should().Be(new TimeOnly(17, 0));
    }

    [Fact]
    public void GetAvailableSlots_OnWeekend_ShouldReturnEmpty()
    {
        var schedule = Schedule.Create(TenantId, UserId, "Working Hours", "UTC").Value;
        var saturday = GetNextSaturday();

        var slots = schedule.GetAvailableSlots(saturday, 30).ToList();

        slots.Should().BeEmpty();
    }

    [Fact]
    public void SetRules_WithCustomRules_ShouldOverrideDefaults()
    {
        var schedule = Schedule.Create(TenantId, UserId, "Custom", "UTC").Value;
        var rules = new[] { (DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0)) };

        var result = schedule.SetRules(rules);

        result.IsSuccess.Should().BeTrue();
        schedule.Rules.Should().HaveCount(1);
        schedule.Rules[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void SetRules_WithInvalidTimeRange_ShouldFail()
    {
        var schedule = Schedule.Create(TenantId, UserId, "Custom", "UTC").Value;
        var badRules = new[] { (DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(10, 0)) };

        var result = schedule.SetRules(badRules);

        result.IsFailure.Should().BeTrue();
    }

    private static DateOnly GetNextMonday()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private static DateOnly GetNextSaturday()
    {
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(1);
        return d;
    }
}
