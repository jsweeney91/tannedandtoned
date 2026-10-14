using Microsoft.EntityFrameworkCore;
using tanningTribe.Models;

namespace tanningTribe.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Products.AnyAsync())
        {
            return;
        }

        db.Products.AddRange(
            new Product { Name = "MT2 Drops", Slug = "golden-glow-bronzer-lotion", Category = "Indoor Tanning Lotions", Price = 10.99m, StockQuantity = 50, IsFeatured = true, ImageUrl = "https://placehold.co/600x600/d4a373/ffffff?text=Golden+Glow", Description = "A rich DHA bronzer blended with cocoa butter and vitamin E for a deep, streak-free indoor tan." },
            new Product { Name = "MT2 Nasals", Slug = "heatwave-tingle-accelerator", Category = "Indoor Tanning Lotions", Price = 10.99m, StockQuantity = 40, IsFeatured = true, ImageUrl = "https://placehold.co/600x600/e76f51/ffffff?text=Heatwave", Description = "High-intensity tingle formula that boosts microcirculation for faster, darker results. Experienced tanners only." },
            new Product { Name = "Retatruide 40mg Pen", Slug = "island-breeze-dark-accelerator", Category = "Weight Loss", Price = 170.00m, StockQuantity = 60, ImageUrl = "https://placehold.co/600x600/f4a261/ffffff?text=Island+Breeze", Description = "Tyrosine-rich accelerator with a tropical coconut scent, ideal for building a base tan." },
            new Product { Name = "Klow 40Mg Pen", Slug = "velvet-bronze-self-tan-mousse", Category = "Weight Loss", Price = 170.00m, StockQuantity = 80, IsFeatured = true, ImageUrl = "https://placehold.co/600x600/b5651d/ffffff?text=Velvet+Bronze", Description = "Fast-drying, quick-developing mousse that delivers a natural medium-to-dark tan in 4 hours." },
            new Product { Name = "Monjaro 40Mg Pen", Slug = "sunless-glow-gradual-tan", Category = "Weight Loss", Price = 170.00m, StockQuantity = 100, ImageUrl = "https://placehold.co/600x600/e9c46a/ffffff?text=Sunless+Glow", Description = "Hydrating daily moisturizer that builds a subtle, buildable tan with every use." },
            new Product { Name = "Monjaro 60Mg Pen", Slug = "tan-extender-after-sun", Category = "Weight Loss", Price = 170.00m, StockQuantity = 70, ImageUrl = "https://placehold.co/600x600/8ab17d/ffffff?text=Tan+Extender", Description = "Aloe vera and hyaluronic acid formula that soothes skin and locks in color for longer-lasting results." },
            new Product { Name = "Retatruide 60mg Pen", Slug = "exfoliating-pre-tan-scrub", Category = "Weight Loss", Price = 170.00m, StockQuantity = 90, ImageUrl = "https://placehold.co/600x600/9c6644/ffffff?text=Pre-Tan+Scrub", Description = "Gentle sugar scrub that preps skin for an even, flawless tan." }
            
        );

        await db.SaveChangesAsync();
    }
}
