using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db)
    : RepositoryBase<User, UserId>(db), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default) =>
        await Db.Users.FirstOrDefaultAsync(u => u.Email.Value == email && u.TenantId == tenantId, ct);

    public async Task<bool> EmailExistsInTenantAsync(string email, TenantId tenantId, CancellationToken ct = default) =>
        await Db.Users.AnyAsync(u => u.Email.Value == email && u.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<User>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await Db.Users.Where(u => u.TenantId == tenantId).ToListAsync(ct);
}
