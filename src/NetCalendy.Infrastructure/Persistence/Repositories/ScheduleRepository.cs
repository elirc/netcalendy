using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.Repositories;
using NetCalendy.Domain.Schedules;
using NetCalendy.Domain.Tenants;
using NetCalendy.Domain.Users;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public sealed class ScheduleRepository(AppDbContext db)
    : RepositoryBase<Schedule, ScheduleId>(db), IScheduleRepository
{
    public async Task<Schedule?> GetDefaultByUserAsync(UserId userId, TenantId tenantId, CancellationToken ct = default) =>
        await Db.Schedules
            .Include("_rules")
            .Where(s => s.UserId == userId && s.TenantId == tenantId)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Schedule>> ListByUserAsync(UserId userId, TenantId tenantId, CancellationToken ct = default) =>
        await Db.Schedules
            .Where(s => s.UserId == userId && s.TenantId == tenantId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}
