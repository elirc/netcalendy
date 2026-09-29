using NetCalendy.Domain.Tenants;

namespace NetCalendy.Domain.Repositories;

public interface ITenantRepository : IRepository<Tenant, TenantId>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
}
