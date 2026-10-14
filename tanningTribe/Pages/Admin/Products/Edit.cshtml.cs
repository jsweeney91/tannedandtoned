using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Products;

public partial class EditModel(StoreDbContext db, ImageService images) : PageModel
{
    public class InputModel
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(140)]
        public string? Slug { get; set; }

        [Required, MaxLength(60)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Price { get; set; }

        [Range(0, 1_000_000)]
        public int StockQuantity { get; set; }

        public bool IsFeatured { get; set; }

        public string ImageUrl { get; set; } = string.Empty;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ImageFile { get; set; }

    [BindProperty]
    [MaxLength(500)]
    public string? ImageUrlOverride { get; set; }

    public bool IsNew => Input.Id == 0;
    public List<string> Categories { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadCategoriesAsync();

        if (id is null)
        {
            return Page();
        }

        var product = await db.Products.FindAsync(id.Value);
        if (product is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Category = product.Category,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsFeatured = product.IsFeatured,
            ImageUrl = product.ImageUrl
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();

        var slug = string.IsNullOrWhiteSpace(Input.Slug) ? Slugify(Input.Name) : Slugify(Input.Slug);
        if (slug.Length == 0)
        {
            ModelState.AddModelError("Input.Slug", "Could not generate a URL slug; please enter one.");
        }
        else if (await db.Products.AnyAsync(p => p.Slug == slug && p.Id != Input.Id))
        {
            ModelState.AddModelError("Input.Slug", "That slug is already used by another product.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Product product;
        if (IsNew)
        {
            product = new Product();
            db.Products.Add(product);
        }
        else
        {
            var existing = await db.Products.FindAsync(Input.Id);
            if (existing is null)
            {
                return NotFound();
            }
            product = existing;
        }

        var previousImage = product.ImageUrl;
        var newImageUrl = Input.ImageUrl;

        if (ImageFile is { Length: > 0 })
        {
            try
            {
                newImageUrl = await images.SaveProductImageAsync(ImageFile);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("ImageFile", ex.Message);
                return Page();
            }
        }
        else if (!string.IsNullOrWhiteSpace(ImageUrlOverride))
        {
            newImageUrl = ImageUrlOverride.Trim();
        }

        product.Name = Input.Name.Trim();
        product.Slug = slug;
        product.Category = Input.Category.Trim();
        product.Description = Input.Description.Trim();
        product.Price = Input.Price;
        product.StockQuantity = Input.StockQuantity;
        product.IsFeatured = Input.IsFeatured;
        product.ImageUrl = newImageUrl;

        await db.SaveChangesAsync();

        if (!string.Equals(previousImage, newImageUrl, StringComparison.Ordinal))
        {
            images.DeleteIfLocal(previousImage);
        }

        TempData["Message"] = IsNew ? $"\"{product.Name}\" was created." : $"\"{product.Name}\" was updated.";
        return RedirectToPage("/Admin/Products/Index");
    }

    private async Task LoadCategoriesAsync() =>
        Categories = await db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();

    private static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        lower = NonSlugChars().Replace(lower, "-");
        return lower.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
