using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Orders;

public class DetailsModel(StoreDbContext db, WhatsAppService whatsApp, OrderWorkflowService workflow) : PageModel
{
    public Order Order { get; set; } = default!;
    public ReferralCode? Referral { get; set; }
    public string MessageTitle { get; set; } = "";
    public string MessagePreview { get; set; } = "";
    public string WhatsAppLink { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        Referral = await db.ReferralCodes.FirstOrDefaultAsync(r => r.OwnerOrderId == order.Id);
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

    private async Task<IActionResult> RunAsync(int id, Task<WorkflowResult> action)
    {
        var result = await action;
        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return RedirectToPage(new { id });
    }
}
