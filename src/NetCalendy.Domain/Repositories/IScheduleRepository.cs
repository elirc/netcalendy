using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Domain.Repositories;

public interface IScheduleRepository : IRepository<Schedule, ScheduleId>
{
    Task<Schedule?> GetDefaultByUserAsync(UserId userId, TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Schedule>> ListByUserAsync(UserId userId, TenantId tenantId, CancellationToken ct = default);
}
