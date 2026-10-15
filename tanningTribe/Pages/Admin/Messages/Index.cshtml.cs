using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Pages.Admin.Messages;

public class IndexModel(StoreDbContext db) : PageModel
{
    public List<Conversation> Conversations { get; set; } = [];

    public async Task OnGetAsync()
    {
        Conversations = await db.Conversations
            .Include(c => c.Order)
            .OrderByDescending(c => c.LastMessageUtc)
            .Take(200)
            .ToListAsync();
    }
}
