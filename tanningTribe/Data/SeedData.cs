using Microsoft.EntityFrameworkCore;

namespace tanningTribe.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        await db.Database.MigrateAsync();
    }
}
