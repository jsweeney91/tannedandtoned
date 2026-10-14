using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public class ReferralCode
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string OwnerEmail { get; set; } = string.Empty;

    public int OwnerOrderId { get; set; }
    public Order? OwnerOrder { get; set; }

    public int DiscountPercent { get; set; } = 10;

    public int TimesUsed { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
