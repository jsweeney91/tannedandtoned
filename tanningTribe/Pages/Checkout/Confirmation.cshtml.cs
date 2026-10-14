using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Checkout;

public class ConfirmationModel(StoreDbContext db) : PageModel
{
    private const string RecentOrdersKey = "RecentOrderIds";

    public Order Order { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        // Only allow the customer who just placed the order (same session) to view it.
        var recent = HttpContext.Session.GetString(RecentOrdersKey) ?? string.Empty;
        var ids = recent.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (!ids.Contains(id.ToString()) && User.Identity?.IsAuthenticated != true)
        {
            return NotFound();
        }

        Order = order;
        return Page();
    }

    public static void RememberOrder(ISession session, int orderId)
    {
        var recent = session.GetString(RecentOrdersKey) ?? string.Empty;
        session.SetString(RecentOrdersKey, string.IsNullOrEmpty(recent) ? orderId.ToString() : $"{recent},{orderId}");
    }
}
