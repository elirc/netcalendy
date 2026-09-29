using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Bookings;

public sealed class Booking : AggregateRoot<BookingId>
{
    private Booking(
        BookingId id,
        TenantId tenantId,
        EventTypeId eventTypeId,
        UserId hostId,
        Invitee invitee,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
        : base(id)
    {
        TenantId = tenantId;
        EventTypeId = eventTypeId;
        HostId = hostId;
        Invitee = invitee;
        StartTime = startTime;
        EndTime = endTime;
        Status = BookingStatus.Confirmed;
        CancellationToken = Guid.NewGuid().ToString("N")[..16];
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public TenantId TenantId { get; }
    public EventTypeId EventTypeId { get; }
    public UserId HostId { get; }
    public Invitee Invitee { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public BookingStatus Status { get; private set; }
    public string CancellationToken { get; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<Booking> Create(
        TenantId tenantId,
        EventTypeId eventTypeId,
        UserId hostId,
        Invitee invitee,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        if (startTime >= endTime)
            return Result.Failure<Booking>(Error.Validation("Booking.InvalidTimeRange", "Start must be before end."));

        if (startTime < DateTimeOffset.UtcNow)
            return Result.Failure<Booking>(Error.Validation("Booking.InThePast", "Cannot book a time in the past."));

        var booking = new Booking(BookingId.New(), tenantId, eventTypeId, hostId, invitee, startTime, endTime);
        booking.RaiseDomainEvent(new BookingCreatedEvent(booking.Id, booking.TenantId, booking.HostId, booking.Invitee.Email));
        return Result.Success(booking);
    }

    public Result Cancel(string? reason = null)
    {
        if (Status == BookingStatus.Cancelled)
            return Result.Failure(Error.Conflict("Booking.AlreadyCancelled", "Booking is already cancelled."));

        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        RaiseDomainEvent(new BookingCancelledEvent(Id, TenantId, HostId, Invitee.Email));
        return Result.Success();
    }

    public bool ConflictsWith(DateTimeOffset start, DateTimeOffset end) =>
        Status != BookingStatus.Cancelled && StartTime < end && EndTime > start;
}
