using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Admin;

public class IndexModel(StoreDbContext db) : PageModel
{
    public decimal Revenue { get; private set; }
    public int PaidOrders { get; private set; }
    public int PendingApproval { get; private set; }
    public int ReferralCodes { get; private set; }
    public int ReferralUses { get; private set; }
    public List<Order> RecentOrders { get; private set; } = [];
    public List<Product> LowStock { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var paid = db.Orders.Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped);
        Revenue = await paid.SumAsync(o => (decimal?)o.Total) ?? 0m;
        PaidOrders = await paid.CountAsync();
        PendingApproval = await db.Orders.CountAsync(o => o.Status == OrderStatus.PendingApproval);
        ReferralCodes = await db.ReferralCodes.CountAsync();
        ReferralUses = await db.ReferralCodes.SumAsync(r => (int?)r.TimesUsed) ?? 0;
        RecentOrders = await db.Orders.OrderByDescending(o => o.CreatedUtc).Take(8).ToListAsync();
        LowStock = await db.Products.Where(p => p.StockQuantity <= 5).OrderBy(p => p.StockQuantity).Take(8).ToListAsync();
    }
}
