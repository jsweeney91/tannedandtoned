using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Reviews;

public class IndexModel(StoreDbContext db) : PageModel
{
    public class InputModel
    {
        [Required, MaxLength(100), Display(Name = "Your name")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress, MaxLength(200), Display(Name = "Email (not shown publicly)")]
        public string? Email { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        [Required, MaxLength(1000), Display(Name = "Your review")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Product (optional)")]
        public int? ProductId { get; set; }
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public List<Review> Reviews { get; private set; } = [];
    public List<SelectListItem> ProductOptions { get; private set; } = [];
    public double AverageRating { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        if (Input.ProductId is int productId && !await db.Products.AnyAsync(p => p.Id == productId))
        {
            Input.ProductId = null;
        }

        db.Reviews.Add(new Review
        {
            CustomerName = Input.Name.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            Rating = Input.Rating,
            Body = Input.Body.Trim(),
            ProductId = Input.ProductId
        });
        await db.SaveChangesAsync();

        StatusMessage = "Thank you! Your review has been sent to us and will appear once it's been approved.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Reviews = await db.Reviews
            .Include(r => r.Product)
            .Where(r => r.IsApproved)
            .OrderByDescending(r => r.IsFeatured)
            .ThenByDescending(r => r.CreatedUtc)
            .ToListAsync();

        AverageRating = Reviews.Count == 0 ? 0 : Math.Round(Reviews.Average(r => r.Rating), 1);

        ProductOptions = await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();
    }
}
