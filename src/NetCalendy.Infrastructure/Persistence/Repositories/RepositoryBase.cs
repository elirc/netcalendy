using Microsoft.EntityFrameworkCore;
using NetCalendy.Domain.Common;
using NetCalendy.Domain.Repositories;

namespace NetCalendy.Infrastructure.Persistence.Repositories;

public abstract class RepositoryBase<TAggregate, TId>(AppDbContext db)
    : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected readonly AppDbContext Db = db;
    protected DbSet<TAggregate> Set => Db.Set<TAggregate>();

    public async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    public void Add(TAggregate aggregate) => Set.Add(aggregate);
    public void Update(TAggregate aggregate) => Set.Update(aggregate);
    public void Remove(TAggregate aggregate) => Set.Remove(aggregate);
}
