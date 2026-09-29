using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public sealed class EventTypeRepository(AppDbContext db)
    : RepositoryBase<EventType, EventTypeId>(db), IEventTypeRepository
{
    public async Task<EventType?> GetBySlugAsync(string slug, UserId ownerId, TenantId tenantId, CancellationToken ct = default) =>
        await Db.EventTypes.FirstOrDefaultAsync(
            e => e.Slug == slug && e.OwnerId == ownerId && e.TenantId == tenantId, ct);

    public async Task<bool> SlugExistsForUserAsync(string slug, UserId ownerId, TenantId tenantId, CancellationToken ct = default) =>
        await Db.EventTypes.AnyAsync(
            e => e.Slug == slug && e.OwnerId == ownerId && e.TenantId == tenantId && e.IsActive, ct);

    public async Task<IReadOnlyList<EventType>> ListByOwnerAsync(UserId ownerId, TenantId tenantId, CancellationToken ct = default) =>
        await Db.EventTypes
            .Where(e => e.OwnerId == ownerId && e.TenantId == tenantId)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
}
