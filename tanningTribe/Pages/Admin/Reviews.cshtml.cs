using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Admin;

public class ReviewsModel(StoreDbContext db) : PageModel
{
    public List<Review> Reviews { get; private set; } = [];
    public int PendingCount { get; private set; }

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Reviews = await db.Reviews
            .Include(r => r.Product)
            .OrderBy(r => r.IsApproved)
            .ThenByDescending(r => r.CreatedUtc)
            .ToListAsync();
        PendingCount = Reviews.Count(r => !r.IsApproved);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var review = await db.Reviews.FindAsync(id);
        if (review is not null)
        {
            review.IsApproved = !review.IsApproved;
            if (!review.IsApproved)
            {
                review.IsFeatured = false;
            }
            await db.SaveChangesAsync();
            Message = review.IsApproved ? $"Review from {review.CustomerName} is now live." : $"Review from {review.CustomerName} hidden.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostFeatureAsync(int id)
    {
        var review = await db.Reviews.FindAsync(id);
        if (review is not null)
        {
            review.IsFeatured = !review.IsFeatured;
            if (review.IsFeatured)
            {
                review.IsApproved = true;
            }
            await db.SaveChangesAsync();
            Message = review.IsFeatured ? $"Review from {review.CustomerName} featured on the home page." : $"Review from {review.CustomerName} no longer featured.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var review = await db.Reviews.FindAsync(id);
        if (review is not null)
        {
            db.Reviews.Remove(review);
            await db.SaveChangesAsync();
            Message = $"Review from {review.CustomerName} deleted.";
        }
        return RedirectToPage();
    }
}
