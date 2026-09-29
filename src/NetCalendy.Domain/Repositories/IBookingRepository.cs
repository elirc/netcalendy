using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Repositories;

public interface IBookingRepository : IRepository<Booking, BookingId>
{
    Task<Booking?> GetByCancellationTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetConflictingBookingsAsync(UserId hostId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> ListByHostAsync(UserId hostId, TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> ListByEventTypeAsync(EventTypeId eventTypeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
