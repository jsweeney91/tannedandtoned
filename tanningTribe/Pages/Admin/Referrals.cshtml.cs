using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin;

public class ReferralsModel(StoreDbContext db, ReferralService referrals) : PageModel
{
    public class NewCodeInput
    {
        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20), Display(Name = "Code")]
        public string? Code { get; set; }

        [Range(1, 100), Display(Name = "Discount %")]
        public int DiscountPercent { get; set; }
    }

    [BindProperty]
    public NewCodeInput NewCode { get; set; } = new();

    [BindProperty, Range(1, 100)]
    public int DefaultDiscountPercent { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public List<ReferralCode> Codes { get; private set; } = [];
    public int TotalUses { get; private set; }
    public int ActiveReferrers { get; private set; }
    public int CustomersWithoutCode { get; private set; }

    public async Task OnGetAsync()
    {
        DefaultDiscountPercent = await referrals.GetDefaultDiscountPercentAsync();
        NewCode.DiscountPercent = DefaultDiscountPercent;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSaveDefaultAsync()
    {
        if (DefaultDiscountPercent is < 1 or > 100)
        {
            StatusMessage = "Default discount must be between 1 and 100.";
            return RedirectToPage();
        }

        await referrals.SetDefaultDiscountPercentAsync(DefaultDiscountPercent);
        StatusMessage = $"Default referral discount set to {DefaultDiscountPercent}%.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ModelState.Remove(nameof(DefaultDiscountPercent));
        if (!ModelState.IsValid)
        {
            DefaultDiscountPercent = await referrals.GetDefaultDiscountPercentAsync();
            await LoadAsync();
            return Page();
        }

        var (referral, error) = await referrals.CreateAsync(NewCode.Email, NewCode.DiscountPercent, NewCode.Code);
        if (referral is null)
        {
            ModelState.AddModelError("NewCode.Email", error ?? "Could not create code.");
            DefaultDiscountPercent = await referrals.GetDefaultDiscountPercentAsync();
            await LoadAsync();
            return Page();
        }

        StatusMessage = $"Created code {referral.Code} for {referral.OwnerEmail}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, int discountPercent)
    {
        if (discountPercent is < 1 or > 100)
        {
            StatusMessage = "Discount must be between 1 and 100.";
        }
        else if (await referrals.UpdateDiscountAsync(id, discountPercent))
        {
            StatusMessage = $"Discount updated to {discountPercent}%.";
        }
        return RedirectToPage();
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

    public async Task<IActionResult> OnPostBackfillAsync()
    {
        var created = await referrals.BackfillApprovedCustomersAsync();
        StatusMessage = created == 0 ? "All paid customers already have a code." : $"Created {created} code(s) for existing customers.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Codes = await db.ReferralCodes.Include(r => r.OwnerOrder).OrderByDescending(r => r.TimesUsed).ThenByDescending(r => r.CreatedUtc).ToListAsync();
        TotalUses = Codes.Sum(r => r.TimesUsed);
        ActiveReferrers = Codes.Count(r => r.TimesUsed > 0);

        var owners = Codes.Select(r => r.OwnerEmail).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var paidEmails = await db.Orders
            .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped)
            .Select(o => o.CustomerEmail)
            .Distinct()
            .ToListAsync();
        CustomersWithoutCode = paidEmails.Count(e => !owners.Contains(ReferralService.NormalizeEmail(e)));
    }
}
