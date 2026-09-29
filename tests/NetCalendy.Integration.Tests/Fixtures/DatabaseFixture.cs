using Microsoft.Extensions.DependencyInjection;
using NetCalendy.Infrastructure.Persistence;

namespace NetCalendy.Integration.Tests.Fixtures;

public static class DatabaseFixture
{
    public static async Task ResetAsync(IntegrationTestWebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public static async Task SeedAsync(IntegrationTestWebAppFactory factory, Func<AppDbContext, Task> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }
}
