using FluentAssertions;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using Xunit;

namespace NetCalendy.Domain.Tests.BookingTests;

public sealed class BookingAggregateTests
{
    private static readonly TenantId TenantId = TenantId.New();
    private static readonly UserId HostId = UserId.New();
    private static readonly EventTypeId EventTypeId = EventTypeId.New();

    [Fact]
    public void Create_WithValidData_ShouldSucceedAndRaiseEvent()
    {
        var invitee = Invitee.Create("Alice Smith", "alice@example.com", "America/New_York").Value;
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Booking.Create(TenantId, EventTypeId, HostId, invitee, start, start.AddMinutes(30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BookingStatus.Confirmed);
        result.Value.DomainEvents.Should().ContainSingle(e => e is BookingCreatedEvent);
    }

    [Fact]
    public void Create_WithStartInThePast_ShouldFail()
    {
        var invitee = Invitee.Create("Alice Smith", "alice@example.com", "UTC").Value;
        var past = DateTimeOffset.UtcNow.AddHours(-1);

        var result = Booking.Create(TenantId, EventTypeId, HostId, invitee, past, past.AddMinutes(30));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.InThePast");
    }

    [Fact]
    public void Create_WithEndBeforeStart_ShouldFail()
    {
        var invitee = Invitee.Create("Alice Smith", "alice@example.com", "UTC").Value;
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var result = Booking.Create(TenantId, EventTypeId, HostId, invitee, start, start.AddMinutes(-1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.InvalidTimeRange");
    }

    [Fact]
    public void Cancel_ShouldChangeStatusAndRaiseEvent()
    {
        var booking = CreateValidBooking();

        var result = booking.Cancel("No longer needed");

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("No longer needed");
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCancelledEvent);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldFail()
    {
        var booking = CreateValidBooking();
        booking.Cancel();

        var result = booking.Cancel("Again");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.AlreadyCancelled");
    }

    [Fact]
    public void ConflictsWith_OverlappingTimeRange_ShouldReturnTrue()
    {
        var booking = CreateValidBooking();
        var start = booking.StartTime.AddMinutes(-15);
        var end = booking.StartTime.AddMinutes(15);

        booking.ConflictsWith(start, end).Should().BeTrue();
    }

    [Fact]
    public void ConflictsWith_NonOverlappingTimeRange_ShouldReturnFalse()
    {
        var booking = CreateValidBooking();
        var start = booking.EndTime.AddMinutes(1);
        var end = booking.EndTime.AddMinutes(31);

        booking.ConflictsWith(start, end).Should().BeFalse();
    }

    [Fact]
    public void ConflictsWith_AfterCancellation_ShouldReturnFalse()
    {
        var booking = CreateValidBooking();
        booking.Cancel();

        booking.ConflictsWith(booking.StartTime, booking.EndTime).Should().BeFalse();
    }

    private static Booking CreateValidBooking()
    {
        var invitee = Invitee.Create("Alice Smith", "alice@example.com", "UTC").Value;
        var start = DateTimeOffset.UtcNow.AddDays(1);
        return Booking.Create(TenantId, EventTypeId, HostId, invitee, start, start.AddMinutes(30)).Value;
    }
}
