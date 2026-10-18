using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public class ReferralCode
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string OwnerEmail { get; set; } = string.Empty;

    /// <summary>The order that first earned this code, if any. Codes created by the owner in admin have none.</summary>
    public int? OwnerOrderId { get; set; }
    public Order? OwnerOrder { get; set; }

    [Range(1, 100)]
    public int DiscountPercent { get; set; } = 10;

    public int TimesUsed { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
