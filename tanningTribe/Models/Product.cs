using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(140)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int StockQuantity { get; set; }

    public bool IsFeatured { get; set; }
}
