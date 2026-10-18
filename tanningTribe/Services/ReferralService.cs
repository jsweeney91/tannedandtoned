using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Services;

public class ReferralService(StoreDbContext db)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int FallbackDefaultDiscountPercent = 10;

    public static string Normalize(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    public async Task<StoreSettings> GetSettingsAsync()
    {
        var settings = await db.StoreSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new StoreSettings { DefaultReferralDiscountPercent = FallbackDefaultDiscountPercent };
            db.StoreSettings.Add(settings);
            await db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task<int> GetDefaultDiscountPercentAsync() =>
        (await GetSettingsAsync()).DefaultReferralDiscountPercent;

    public async Task SetDefaultDiscountPercentAsync(int percent)
    {
        var settings = await GetSettingsAsync();
        settings.DefaultReferralDiscountPercent = Math.Clamp(percent, 1, 100);
        await db.SaveChangesAsync();
    }

    public async Task<ReferralCode?> ValidateAsync(string? code, string customerEmail)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return null;
        }

        var referral = await db.ReferralCodes
            .FirstOrDefaultAsync(r => r.Code == normalized && r.IsActive);

        if (referral is null)
        {
            return null;
        }

        if (string.Equals(referral.OwnerEmail, NormalizeEmail(customerEmail), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return referral;
    }

    /// <summary>Returns the customer's existing code (matched by email) or creates one. A customer only ever has one code.</summary>
    public async Task<ReferralCode> GetOrCreateForOrderAsync(Order order)
    {
        var existing = await FindByEmailAsync(order.CustomerEmail);
        if (existing is not null)
        {
            return existing;
        }

        return await CreateAsync(order.CustomerEmail, await GetDefaultDiscountPercentAsync(), order.Id);
    }

    public Task<ReferralCode?> FindByEmailAsync(string? email)
    {
        var normalized = NormalizeEmail(email);
        return db.ReferralCodes.FirstOrDefaultAsync(r => r.OwnerEmail == normalized);
    }

    /// <summary>Creates a code for an email address. If <paramref name="code"/> is empty a random one is generated.</summary>
    public async Task<(ReferralCode? Referral, string? Error)> CreateAsync(string? email, int discountPercent, string? code)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
        {
            return (null, "An email address is required.");
        }

        if (await FindByEmailAsync(normalizedEmail) is not null)
        {
            return (null, $"{normalizedEmail} already has a referral code.");
        }

        var normalizedCode = Normalize(code);
        if (normalizedCode.Length > 0)
        {
            if (normalizedCode.Length > 20)
            {
                return (null, "Codes must be 20 characters or fewer.");
            }

            if (await db.ReferralCodes.AnyAsync(r => r.Code == normalizedCode))
            {
                return (null, $"The code {normalizedCode} is already in use.");
            }
        }

        var referral = await CreateAsync(normalizedEmail, discountPercent, ownerOrderId: null, normalizedCode.Length > 0 ? normalizedCode : null);
        return (referral, null);
    }

    private async Task<ReferralCode> CreateAsync(string email, int discountPercent, int? ownerOrderId, string? code = null)
    {
        if (code is null)
        {
            do
            {
                code = Generate();
            } while (await db.ReferralCodes.AnyAsync(r => r.Code == code));
        }

        var referral = new ReferralCode
        {
            Code = code,
            OwnerEmail = NormalizeEmail(email),
            OwnerOrderId = ownerOrderId,
            DiscountPercent = Math.Clamp(discountPercent, 1, 100)
        };

        db.ReferralCodes.Add(referral);
        await db.SaveChangesAsync();
        return referral;
    }

    public async Task<bool> UpdateDiscountAsync(int id, int discountPercent)
    {
        var referral = await db.ReferralCodes.FindAsync(id);
        if (referral is null)
        {
            return false;
        }

        referral.DiscountPercent = Math.Clamp(discountPercent, 1, 100);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Creates codes for every customer with a paid/shipped order who doesn't have one yet. Returns how many were created.</summary>
    public async Task<int> BackfillApprovedCustomersAsync()
    {
        var emails = await db.Orders
            .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped)
            .OrderBy(o => o.CreatedUtc)
            .Select(o => new { o.Id, o.CustomerEmail })
            .ToListAsync();

        var defaultPercent = await GetDefaultDiscountPercentAsync();
        var created = 0;
        foreach (var group in emails.GroupBy(e => NormalizeEmail(e.CustomerEmail)).Where(g => g.Key.Length > 0))
        {
            if (await FindByEmailAsync(group.Key) is null)
            {
                await CreateAsync(group.Key, defaultPercent, group.First().Id);
                created++;
            }
        }

        return created;
    }

    public async Task RecordUseAsync(string? code)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return;
        }

        var referral = await db.ReferralCodes.FirstOrDefaultAsync(r => r.Code == normalized);
        if (referral is not null)
        {
            referral.TimesUsed++;
            await db.SaveChangesAsync();
        }
    }

    private static string Generate()
    {
        var prefix = "TAN";
        var bytes = RandomNumberGenerator.GetBytes(5);
        var chars = bytes.Select(b => Alphabet[b % Alphabet.Length]);
        return prefix + new string(chars.ToArray());
    }
}
