using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

/// <summary>Single-row table of owner-editable store settings.</summary>
public class StoreSettings
{
    public int Id { get; set; }

    [Range(1, 100)]
    public int DefaultReferralDiscountPercent { get; set; } = 10;
}
