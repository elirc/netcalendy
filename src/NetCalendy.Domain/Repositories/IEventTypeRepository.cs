using NetCalendy.Domain.EventTypes;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Repositories;

public interface IEventTypeRepository : IRepository<EventType, EventTypeId>
{
    Task<EventType?> GetBySlugAsync(string slug, UserId ownerId, TenantId tenantId, CancellationToken ct = default);
    Task<bool> SlugExistsForUserAsync(string slug, UserId ownerId, TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EventType>> ListByOwnerAsync(UserId ownerId, TenantId tenantId, CancellationToken ct = default);
}
