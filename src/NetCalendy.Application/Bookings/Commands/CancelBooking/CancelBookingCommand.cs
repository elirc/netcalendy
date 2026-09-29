using MediatR;
using NetCalendy.Domain.Common;

namespace NetCalendy.Application.Bookings.Commands.CancelBooking;

public sealed record CancelBookingCommand(string CancellationToken, string? Reason = null) : IRequest<Result>;
