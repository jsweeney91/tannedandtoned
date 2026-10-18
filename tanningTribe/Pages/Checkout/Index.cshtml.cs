using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using tanningTribe.Data;
using tanningTribe.Models;
using tanningTribe.Services;

namespace tanningTribe.Pages.Checkout;

public class IndexModel(StoreDbContext db, CartService cart, ReferralService referrals, WhatsAppService whatsApp, OrderNumberService orderNumbers, OrderEmailService emails) : PageModel
{
    public class InputModel
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(30), Display(Name = "WhatsApp number")]
        public string WhatsAppNumber { get; set; } = string.Empty;

        [Required, MaxLength(500), Display(Name = "Delivery address")]
        public string ShippingAddress { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ReferralCode { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<CartItem> Items { get; private set; } = [];
    public int DefaultDiscountPercent { get; private set; } = ReferralService.FallbackDefaultDiscountPercent;
    public decimal Subtotal { get; private set; }
    public decimal Postage => Order.StandardPostage;
    public decimal Total => Subtotal + Postage;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Items.Count == 0 ? RedirectToPage("/Cart/Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (Items.Count == 0)
        {
            return RedirectToPage("/Cart/Index");
        }

        var normalizedNumber = whatsApp.NormalizeNumber(Input.WhatsAppNumber);
        if (normalizedNumber.Length < 8)
        {
            ModelState.AddModelError("Input.WhatsAppNumber", "Please enter a valid WhatsApp number.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ReferralCode? referral = null;
        if (!string.IsNullOrWhiteSpace(Input.ReferralCode))
        {
            referral = await referrals.ValidateAsync(Input.ReferralCode, Input.Email);
            if (referral is null)
            {
                ModelState.AddModelError("Input.ReferralCode", "That referral code is invalid or can't be used on this order.");
                return Page();
            }
        }

        var discount = referral is null ? 0m : Math.Round(Subtotal * referral.DiscountPercent / 100m, 2);

        var order = new Order
        {
            OrderNumber = await orderNumbers.NextAsync(),
            CustomerName = Input.Name.Trim(),
            CustomerEmail = Input.Email.Trim(),
            WhatsAppNumber = normalizedNumber,
            ShippingAddress = Input.ShippingAddress.Trim(),
            CustomerNotes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
            Total = Subtotal - discount + Postage,
            DiscountAmount = discount,
            PostageAmount = Postage,
            ReferralCodeUsed = referral?.Code,
            Status = OrderStatus.PendingApproval,
            Items = Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.Name,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await emails.SendReceivedAsync(order);
        await emails.NotifyOwnerNewOrderAsync(order);

        cart.Clear();
        ConfirmationModel.RememberOrder(HttpContext.Session, order.Id);
        return RedirectToPage("/Checkout/Confirmation", new { id = order.Id });
    }

    private async Task LoadAsync()
    {
        Items = cart.GetItems();
        Subtotal = cart.Total();
        DefaultDiscountPercent = await referrals.GetDefaultDiscountPercentAsync();
    }
}
