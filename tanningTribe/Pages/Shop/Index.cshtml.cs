using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Shop;

public class IndexModel(StoreDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    public List<Product> Products { get; private set; } = [];
    public List<string> Categories { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Categories = await db.Products
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var query = db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Category))
        {
            query = query.Where(p => p.Category == Category);
        }

        Products = await query.OrderBy(p => p.Name).ToListAsync();
    }
}
