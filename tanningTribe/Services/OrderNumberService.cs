using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;

namespace tanningTribe.Services;

/// <summary>Generates short, unambiguous order numbers used as the customer's bank transfer reference.</summary>
public class OrderNumberService(StoreDbContext db)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string Prefix = "TT-";
    private const int Length = 6;

    public async Task<string> NextAsync()
    {
        string number;
        do
        {
            number = Generate();
        } while (await db.Orders.AnyAsync(o => o.OrderNumber == number));

        return number;
    }

    private static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(Length);
        return Prefix + new string(bytes.Select(b => Alphabet[b % Alphabet.Length]).ToArray());
    }
}
