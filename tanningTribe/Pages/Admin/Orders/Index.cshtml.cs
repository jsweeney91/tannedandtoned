using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Admin.Orders;

public class IndexModel(StoreDbContext db, OrderWorkflowService workflow) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public OrderStatus? Status { get; set; }

    public List<Order> Orders { get; set; } = [];
    public int PendingCount { get; set; }

    public async Task OnGetAsync()
    {
        PendingCount = await db.Orders.CountAsync(o => o.Status == OrderStatus.PendingApproval);

        var query = db.Orders.Include(o => o.Items).AsQueryable();
        if (Status is not null)
        {
            query = query.Where(o => o.Status == Status);
        }

        Orders = await query.OrderByDescending(o => o.CreatedUtc).Take(200).ToListAsync();
    }

    public Task<IActionResult> OnPostApproveAsync(int id) => RunAsync(id, workflow.ApproveAsync(id));
    public Task<IActionResult> OnPostRejectAsync(int id) => RunAsync(id, workflow.RejectAsync(id), stay: true);
    public Task<IActionResult> OnPostMarkPaidAsync(int id) => RunAsync(id, workflow.MarkPaidAsync(id));
    public Task<IActionResult> OnPostMarkShippedAsync(int id, string? trackingNumber) => RunAsync(id, workflow.MarkShippedAsync(id, trackingNumber));

    private async Task<IActionResult> RunAsync(int id, Task<WorkflowResult> action, bool stay = false)
    {
        var result = await action;
        TempData[result.Success ? "Message" : "Error"] = result.Message;
        return result.Success && !stay
            ? RedirectToPage("/Admin/Orders/Details", new { id })
            : RedirectToPage(new { status = Status });
    }
}
