using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Products;

public class IndexModel(StoreDbContext db, ImageService images) : PageModel
{
    public List<Product> Products { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Products = await db.Products.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var hasOrders = await db.OrderItems.AnyAsync(i => i.ProductId == id);
        if (hasOrders)
        {
            product.StockQuantity = 0;
            product.IsFeatured = false;
            await db.SaveChangesAsync();
            TempData["Message"] = $"\"{product.Name}\" has existing orders so it was set to out of stock instead of deleted.";
            return RedirectToPage();
        }

        await images.DeleteIfOwnedAsync(product.ImageUrl);
        db.Products.Remove(product);
        await db.SaveChangesAsync();
        TempData["Message"] = $"\"{product.Name}\" was deleted.";
        return RedirectToPage();
    }
}
