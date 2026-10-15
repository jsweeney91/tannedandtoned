using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Messages;

public class ThreadModel(StoreDbContext db, ConversationService conversations, ResendService resend) : PageModel
{
    public Conversation Conversation { get; set; } = default!;
    public bool EmailConfigured => resend.IsConfigured;

    [BindProperty]
    public string Reply { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var conversation = await db.Conversations
            .Include(c => c.Order)
            .Include(c => c.Messages.OrderBy(m => m.SentUtc))
            .FirstOrDefaultAsync(c => c.Id == id);
        if (conversation is null)
        {
            return NotFound();
        }

        Conversation = conversation;
        await conversations.MarkReadAsync(conversation);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var result = await conversations.ReplyAsync(id, Reply);
        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostLinkOrderAsync(int id, string? orderNumber)
    {
        var conversation = await db.Conversations.FindAsync(id);
        if (conversation is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            conversation.OrderId = null;
            TempData["Message"] = "Order unlinked.";
        }
        else
        {
            var normalized = orderNumber.Trim().ToUpperInvariant();
            var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == normalized);
            if (order is null)
            {
                TempData["Error"] = $"No order found with number {normalized}.";
                return RedirectToPage(new { id });
            }

            conversation.OrderId = order.Id;
            TempData["Message"] = $"Linked to order {order.OrderNumber}.";
        }

        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
}
