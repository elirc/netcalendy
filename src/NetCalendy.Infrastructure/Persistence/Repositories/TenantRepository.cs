using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(AppDbContext db)
    : RepositoryBase<Tenant, TenantId>(db), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await Db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        await Db.Tenants.AnyAsync(t => t.Slug == slug, ct);
}
