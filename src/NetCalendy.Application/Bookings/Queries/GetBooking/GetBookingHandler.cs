using MediatR;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;

namespace NetCalendy.Application.Bookings.Queries.GetBooking;

public sealed class GetBookingHandler(IBookingRepository bookings)
    : IRequestHandler<GetBookingQuery, Result<BookingDto>>
{
    public async Task<Result<BookingDto>> Handle(GetBookingQuery query, CancellationToken ct)
    {
        var booking = await bookings.GetByIdAsync(BookingId.From(query.BookingId), ct);
        if (booking is null) return Result.Failure<BookingDto>(Error.NotFound("Booking", query.BookingId));

        return Result.Success(MapToDto(booking));
    }

    private static BookingDto MapToDto(Booking b) => new(
        b.Id.Value, b.EventTypeId.Value, b.HostId.Value,
        b.Invitee.Name, b.Invitee.Email, b.Invitee.Timezone, b.Invitee.Notes,
        b.StartTime, b.EndTime, b.Status.ToString(), b.CancellationToken, b.CancellationReason);
}
