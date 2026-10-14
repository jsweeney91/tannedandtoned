using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Cart;

public class IndexModel(StoreDbContext db, CartService cart) : PageModel
{
    public List<CartItem> Items { get; private set; } = [];
    public decimal Total { get; private set; }

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostAddAsync(int productId, int quantity = 1)
    {
        var product = await db.Products.FindAsync(productId);
        if (product is null)
        {
            return NotFound();
        }

        cart.Add(product, Math.Max(1, quantity));
        return RedirectToPage();
    }

    public IActionResult OnPostUpdate(int productId, int quantity)
    {
        cart.UpdateQuantity(productId, quantity);
        return RedirectToPage();
    }

    public IActionResult OnPostRemove(int productId)
    {
        cart.Remove(productId);
        return RedirectToPage();
    }

    private void Load()
    {
        Items = cart.GetItems();
        Total = cart.Total();
    }
}
