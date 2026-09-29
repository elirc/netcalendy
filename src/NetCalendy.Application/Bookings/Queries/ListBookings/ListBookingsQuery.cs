using MediatR;
using NetCalendy.Application.Bookings.Queries.GetBooking;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Bookings.Queries.ListBookings;

public sealed record ListBookingsQuery(
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<Result<IReadOnlyList<BookingDto>>>;
