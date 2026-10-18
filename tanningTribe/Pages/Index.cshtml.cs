using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages
{
    public class IndexModel(StoreDbContext db) : PageModel
    {
        public List<Product> Featured { get; private set; } = [];
        public List<string> Categories { get; private set; } = [];
        public List<Review> FeaturedReviews { get; private set; } = [];

        public async Task OnGetAsync()
        {
            Featured = await db.Products
                .Where(p => p.IsFeatured)
                .OrderBy(p => p.Name)
                .Take(4)
                .ToListAsync();

            FeaturedReviews = await db.Reviews
                .Include(r => r.Product)
                .Where(r => r.IsApproved && r.IsFeatured)
                .OrderByDescending(r => r.CreatedUtc)
                .Take(3)
                .ToListAsync();

            Categories = await db.Products
                .Where(p => p.StockQuantity > 0)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public static string IconFor(string category)
        {
            var c = category.ToLowerInvariant();
            if (c.Contains("self")) return "\u2728";
            if (c.Contains("after") || c.Contains("skin")) return "\u2665";
            if (c.Contains("access")) return "\U0001F576";
            if (c.Contains("lotion") || c.Contains("tan")) return "\u2600";
            return "\u2B50";
        }
    }
}
