using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Repositories;

public interface IUserRepository : IRepository<User, UserId>
{
    Task<User?> GetByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default);
    Task<bool> EmailExistsInTenantAsync(string email, TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
}
