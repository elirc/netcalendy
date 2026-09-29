using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Bookings.Queries.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IRequest<Result<BookingDto>>;

public sealed record BookingDto(
    Guid Id,
    Guid EventTypeId,
    Guid HostId,
    string InviteeName,
    string InviteeEmail,
    string InviteeTimezone,
    string? InviteeNotes,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    string CancellationToken,
    string? CancellationReason);
