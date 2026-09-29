using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Bookings.Commands.CreateBooking;

public sealed class CreateBookingHandler(
    IBookingRepository bookings,
    IEventTypeRepository eventTypes,
    ICurrentTenantService currentTenant,
    IUnitOfWork uow)
    : IRequestHandler<CreateBookingCommand, Result<CreateBookingResponse>>
{
    public async Task<Result<CreateBookingResponse>> Handle(CreateBookingCommand cmd, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var hostId = UserId.From(cmd.HostUserId);

        var eventType = await eventTypes.GetByIdAsync(EventTypeId.From(cmd.EventTypeId), ct);
        if (eventType is null) return Result.Failure<CreateBookingResponse>(Error.NotFound("EventType", cmd.EventTypeId));
        if (!eventType.IsActive) return Result.Failure<CreateBookingResponse>(Error.Validation("EventType.Inactive", "This event type is not available."));

        var endTime = cmd.StartTime.AddMinutes(eventType.Duration.Minutes);

        var conflicts = await bookings.GetConflictingBookingsAsync(hostId, cmd.StartTime, endTime, ct);
        if (conflicts.Count > 0)
            return Result.Failure<CreateBookingResponse>(Error.Conflict("Booking.TimeConflict", "This time slot is no longer available."));

        var inviteeResult = Invitee.Create(cmd.InviteeName, cmd.InviteeEmail, cmd.InviteeTimezone, cmd.Notes);
        if (inviteeResult.IsFailure) return Result.Failure<CreateBookingResponse>(inviteeResult.Error);

        var bookingResult = Booking.Create(tenantId, eventType.Id, hostId, inviteeResult.Value, cmd.StartTime, endTime);
        if (bookingResult.IsFailure) return Result.Failure<CreateBookingResponse>(bookingResult.Error);

        bookings.Add(bookingResult.Value);
        await uow.SaveChangesAsync(ct);

        var b = bookingResult.Value;
        return Result.Success(new CreateBookingResponse(
            b.Id.Value, b.CancellationToken, b.StartTime, b.EndTime, b.Invitee.Name, b.Invitee.Email));
    }
}
