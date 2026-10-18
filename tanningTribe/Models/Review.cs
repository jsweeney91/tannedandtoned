using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public class Review
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Required, MaxLength(1000)]
    public string Body { get; set; } = string.Empty;

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Reviews are hidden until the owner approves them.</summary>
    public bool IsApproved { get; set; }

    /// <summary>Approved reviews flagged as featured are shown on the home page.</summary>
    public bool IsFeatured { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
