using MediatR;
using NetCalendy.Application.Bookings.Queries.GetBooking;
using NetCalendy.Application.Common;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Application.Bookings.Queries.ListBookings;

public sealed class ListBookingsHandler(
    IBookingRepository bookings,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant)
    : IRequestHandler<ListBookingsQuery, Result<IReadOnlyList<BookingDto>>>
{
    public async Task<Result<IReadOnlyList<BookingDto>>> Handle(ListBookingsQuery query, CancellationToken ct)
    {
        var tenantId = TenantId.From(currentTenant.TenantId!.Value);
        var hostId = UserId.From(currentUser.UserId!.Value);

        var list = await bookings.ListByHostAsync(hostId, tenantId, query.From, query.To, ct);

        return Result.Success<IReadOnlyList<BookingDto>>(
            list.Select(MapToDto).ToList());
    }

    private static BookingDto MapToDto(Booking b) => new(
        b.Id.Value, b.EventTypeId.Value, b.HostId.Value,
        b.Invitee.Name, b.Invitee.Email, b.Invitee.Timezone, b.Invitee.Notes,
        b.StartTime, b.EndTime, b.Status.ToString(), b.CancellationToken, b.CancellationReason);
}
