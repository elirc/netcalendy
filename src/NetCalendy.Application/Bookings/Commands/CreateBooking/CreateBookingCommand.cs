using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Bookings.Commands.CreateBooking;

public sealed record CreateBookingCommand(
    Guid EventTypeId,
    Guid HostUserId,
    DateTimeOffset StartTime,
    string InviteeName,
    string InviteeEmail,
    string InviteeTimezone,
    string? Notes = null) : IRequest<Result<CreateBookingResponse>>;

public sealed record CreateBookingResponse(
    Guid BookingId,
    string CancellationToken,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string InviteeName,
    string InviteeEmail);
