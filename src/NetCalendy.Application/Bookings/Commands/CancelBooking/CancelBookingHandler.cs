using MediatR;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;

namespace NetCalendy.Application.Bookings.Commands.CancelBooking;

public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    IUnitOfWork uow)
    : IRequestHandler<CancelBookingCommand, Result>
{
    public async Task<Result> Handle(CancelBookingCommand cmd, CancellationToken ct)
    {
        var booking = await bookings.GetByCancellationTokenAsync(cmd.CancellationToken, ct);
        if (booking is null) return Result.Failure(Error.NotFound("Booking", cmd.CancellationToken));

        var result = booking.Cancel(cmd.Reason);
        if (result.IsFailure) return result;

        bookings.Update(booking);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
