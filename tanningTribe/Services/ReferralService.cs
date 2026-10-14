using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Services;

public class ReferralService(StoreDbContext db)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int DefaultDiscountPercent = 10;

    public static string Normalize(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);

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

        if (string.Equals(referral.OwnerEmail, customerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return referral;
    }

    public async Task<ReferralCode> GetOrCreateForOrderAsync(Order order)
    {
        var existing = await db.ReferralCodes.FirstOrDefaultAsync(r => r.OwnerOrderId == order.Id);
        if (existing is not null)
        {
            return existing;
        }

        string code;
        do
        {
            code = Generate();
        } while (await db.ReferralCodes.AnyAsync(r => r.Code == code));

        var referral = new ReferralCode
        {
            Code = code,
            OwnerEmail = order.CustomerEmail,
            OwnerOrderId = order.Id,
            DiscountPercent = DefaultDiscountPercent
        };

        db.ReferralCodes.Add(referral);
        await db.SaveChangesAsync();
        return referral;
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
