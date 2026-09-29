using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.Bookings;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(AppDbContext db)
    : RepositoryBase<Booking, BookingId>(db), IBookingRepository
{
    public async Task<Booking?> GetByCancellationTokenAsync(string token, CancellationToken ct = default) =>
        await Db.Bookings.FirstOrDefaultAsync(b => b.CancellationToken == token, ct);

    public async Task<IReadOnlyList<Booking>> GetConflictingBookingsAsync(
        UserId hostId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default) =>
        await Db.Bookings
            .Where(b => b.HostId == hostId
                && b.Status != BookingStatus.Cancelled
                && b.StartTime < end
                && b.EndTime > start)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Booking>> ListByHostAsync(
        UserId hostId, TenantId tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await Db.Bookings
            .Where(b => b.HostId == hostId && b.TenantId == tenantId && b.StartTime >= from && b.StartTime <= to)
            .OrderBy(b => b.StartTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Booking>> ListByEventTypeAsync(
        EventTypeId eventTypeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await Db.Bookings
            .Where(b => b.EventTypeId == eventTypeId && b.StartTime >= from && b.StartTime <= to)
            .OrderBy(b => b.StartTime)
            .ToListAsync(ct);
}
