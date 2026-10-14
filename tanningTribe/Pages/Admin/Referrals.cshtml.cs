using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Admin;

public class ReferralsModel(StoreDbContext db) : PageModel
{
    public List<ReferralCode> Codes { get; private set; } = [];
    public int TotalUses { get; private set; }
    public int ActiveReferrers { get; private set; }

    public async Task OnGetAsync()
    {
        Codes = await db.ReferralCodes.Include(r => r.OwnerOrder).OrderByDescending(r => r.TimesUsed).ThenByDescending(r => r.CreatedUtc).ToListAsync();
        TotalUses = Codes.Sum(r => r.TimesUsed);
        ActiveReferrers = Codes.Count(r => r.TimesUsed > 0);
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var code = await db.ReferralCodes.FindAsync(id);
        if (code is not null)
        {
            code.IsActive = !code.IsActive;
            await db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
