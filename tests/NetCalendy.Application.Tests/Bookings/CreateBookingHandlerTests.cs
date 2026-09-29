using FluentAssertions;
using NSubstitute;
using NetCalendy.Application.Bookings.Commands.CreateBooking;
using NetCalendy.Application.Common;
using NetCalendy.Application.Tests.Common;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;
using Xunit;

namespace NetCalendy.Application.Tests.Bookings;

public sealed class CreateBookingHandlerTests
{
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IEventTypeRepository _eventTypes = Substitute.For<IEventTypeRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _hostId = Guid.NewGuid();
    private readonly Guid _eventTypeId = Guid.NewGuid();

    private CreateBookingHandler CreateHandler() => new(
        _bookings,
        _eventTypes,
        new MockCurrentTenantService(_tenantId),
        _uow);

    [Fact]
    public async Task Handle_WithNoConflicts_ShouldCreateBooking()
    {
        var et = SetupEventType();
        _bookings.GetConflictingBookingsAsync(Arg.Any<UserId>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>())
            .Returns([]);

        var start = DateTimeOffset.UtcNow.AddDays(1).AddHours(2);
        var command = new CreateBookingCommand(
            _eventTypeId, _hostId, start,
            "Alice Smith", "alice@example.com", "America/New_York");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InviteeName.Should().Be("Alice Smith");
        _bookings.Received(1).Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_WithConflictingBooking_ShouldReturnConflict()
    {
        var et = SetupEventType();
        var start = DateTimeOffset.UtcNow.AddDays(1).AddHours(2);

        var existingInvitee = Domain.Bookings.Invitee.Create("Bob", "bob@example.com", "UTC").Value;
        var existingBooking = Booking.Create(
            TenantId.From(_tenantId), EventTypeId.From(_eventTypeId), UserId.From(_hostId),
            existingInvitee, start, start.AddMinutes(30)).Value;

        _bookings.GetConflictingBookingsAsync(Arg.Any<UserId>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>())
            .Returns([existingBooking]);

        var command = new CreateBookingCommand(
            _eventTypeId, _hostId, start,
            "Alice Smith", "alice@example.com", "UTC");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.TimeConflict");
    }

    [Fact]
    public async Task Handle_WhenEventTypeNotFound_ShouldReturnNotFound()
    {
        _eventTypes.GetByIdAsync(Arg.Any<EventTypeId>()).Returns((EventType?)null);

        var command = new CreateBookingCommand(
            Guid.NewGuid(), _hostId, DateTimeOffset.UtcNow.AddDays(1),
            "Alice", "alice@example.com", "UTC");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().EndWith(".NotFound");
    }

    private EventType SetupEventType()
    {
        var et = EventType.Create(
            TenantId.From(_tenantId), UserId.From(_hostId),
            "30 Min Chat", "30-min-chat", 30).Value;

        _eventTypes.GetByIdAsync(EventTypeId.From(_eventTypeId)).Returns(et);
        return et;
    }
}
