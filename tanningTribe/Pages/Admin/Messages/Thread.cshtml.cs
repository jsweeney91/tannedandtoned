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

    public Dictionary<int, List<AttachmentInfo>> Attachments { get; set; } = [];

    public record AttachmentInfo(int Id, string FileName, string ContentType, string? ContentId, bool IsInline, long Size);

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

        var messageIds = conversation.Messages.Select(m => m.Id).ToList();
        Attachments = (await db.EmailAttachments
                .Where(a => messageIds.Contains(a.EmailMessageId))
                .Select(a => new { a.EmailMessageId, Info = new AttachmentInfo(a.Id, a.FileName, a.ContentType, a.ContentId, a.IsInline, a.Size) })
                .ToListAsync())
            .GroupBy(x => x.EmailMessageId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Info).ToList());

        Conversation = conversation;
        await conversations.MarkReadAsync(conversation);
        return Page();
    }

    public async Task<IActionResult> OnGetAttachmentAsync(int id, int attachmentId, bool download = false)
    {
        var attachment = await db.EmailAttachments
            .Include(a => a.EmailMessage)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.EmailMessage!.ConversationId == id);
        if (attachment is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return download
            ? File(attachment.Data, attachment.ContentType, attachment.FileName)
            : File(attachment.Data, attachment.ContentType);
    }

    /// <summary>Rewrites cid: image references in an HTML body to the attachment route.</summary>
    public string RenderHtml(Models.EmailMessage message)
    {
        var html = message.HtmlBody ?? string.Empty;
        if (!Attachments.TryGetValue(message.Id, out var list))
        {
            return html;
        }

        foreach (var a in list.Where(a => !string.IsNullOrEmpty(a.ContentId)))
        {
            var url = Url.Page("/Admin/Messages/Thread", "Attachment", new { id = Conversation.Id, attachmentId = a.Id }) ?? string.Empty;
            html = html.Replace($"cid:{a.ContentId}", url, StringComparison.OrdinalIgnoreCase);
        }

        return html;
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
