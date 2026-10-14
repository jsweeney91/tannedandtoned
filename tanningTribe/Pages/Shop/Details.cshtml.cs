using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Shop;

public class DetailsModel(StoreDbContext db) : PageModel
{
    public Product Product { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == slug);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }
}
