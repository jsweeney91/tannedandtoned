using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Orders;

public class DetailsModel(StoreDbContext db, WhatsAppService whatsApp, OrderWorkflowService workflow, RoyalMailService royalMail) : PageModel
{
    public Order Order { get; set; } = default!;
    public ReferralCode? Referral { get; set; }
    public string MessageTitle { get; set; } = "";
    public string MessagePreview { get; set; } = "";
    public string WhatsAppLink { get; set; } = "";
    public bool RoyalMailConfigured => royalMail.IsConfigured;
    public List<Conversation> Conversations { get; set; } = [];
    public List<RoyalMailServiceOption> RoyalMailServices { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        Referral = await db.ReferralCodes.FirstOrDefaultAsync(r => r.OwnerOrderId == order.Id);
        Conversations = await db.Conversations.Where(c => c.OrderId == order.Id).OrderByDescending(c => c.LastMessageUtc).ToListAsync();
        if (royalMail.IsConfigured && order.RoyalMailOrderId is null && order.Status is OrderStatus.Paid or OrderStatus.Shipped)
        {
            RoyalMailServices = await royalMail.GetServicesAsync();
        }
        (MessageTitle, MessagePreview) = order.Status switch
        {
            OrderStatus.PendingApproval => ("Order received (no payment details)", whatsApp.BuildReceivedMessage(order)),
            OrderStatus.Approved => ("Approved + payment details", whatsApp.BuildApprovalMessage(order)),
            OrderStatus.Paid => ("Payment received + referral code", whatsApp.BuildPaidMessage(order, Referral)),
            OrderStatus.Shipped => ("Shipped notification", whatsApp.BuildShippedMessage(order)),
            OrderStatus.Rejected => ("Order declined", whatsApp.BuildRejectedMessage(order)),
            _ => ("Message", string.Empty)
        };
        WhatsAppLink = whatsApp.BuildLink(order.WhatsAppNumber, MessagePreview);
        return Page();
    }

    public Task<IActionResult> OnPostApproveAsync(int id) => RunAsync(id, workflow.ApproveAsync(id));
    public Task<IActionResult> OnPostRejectAsync(int id) => RunAsync(id, workflow.RejectAsync(id));
    public Task<IActionResult> OnPostReopenAsync(int id) => RunAsync(id, workflow.ReopenAsync(id));
    public Task<IActionResult> OnPostMarkPaidAsync(int id) => RunAsync(id, workflow.MarkPaidAsync(id));
    public Task<IActionResult> OnPostMarkShippedAsync(int id, string? trackingNumber) => RunAsync(id, workflow.MarkShippedAsync(id, trackingNumber));
    public Task<IActionResult> OnPostUpdateTrackingAsync(int id, string? trackingNumber) => RunAsync(id, workflow.UpdateTrackingAsync(id, trackingNumber));
    public Task<IActionResult> OnPostMarkSentAsync(int id) => RunAsync(id, workflow.MarkWhatsAppSentAsync(id));

    public async Task<IActionResult> OnPostCreateLabelAsync(int id, int? weightGrams, string? serviceCode)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        var result = await royalMail.CreateOrderAsync(order, weightGrams, serviceCode);
        if (result.Success)
        {
            order.RoyalMailOrderId = result.OrderIdentifier;
            if (!string.IsNullOrWhiteSpace(result.TrackingNumber) && string.IsNullOrWhiteSpace(order.TrackingNumber))
            {
                order.TrackingNumber = result.TrackingNumber;
            }
            await db.SaveChangesAsync();
        }

        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetLabelAsync(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order?.RoyalMailOrderId is null)
        {
            return NotFound();
        }

        var pdf = await royalMail.GetLabelPdfAsync(order.RoyalMailOrderId);
        if (pdf is null)
        {
            TempData["Error"] = "Could not download the label from Royal Mail. It may not be paid for yet - check Click & Drop.";
            return RedirectToPage(new { id });
        }

        return File(pdf, "application/pdf", $"label-{order.OrderNumber}.pdf");
    }

    private async Task<IActionResult> RunAsync(int id, Task<WorkflowResult> action)
    {
        var result = await action;
        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return RedirectToPage(new { id });
    }
}
