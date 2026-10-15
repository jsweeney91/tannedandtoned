using Microsoft.EntityFrameworkCore;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Services;

public record WorkflowResult(bool Success, string Message);

public class OrderWorkflowService(StoreDbContext db, ReferralService referrals, OrderEmailService emails)
{
    public async Task<WorkflowResult> ApproveAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        if (order.Status != OrderStatus.PendingApproval)
        {
            return new(false, $"Order {order.OrderNumber} is not pending approval.");
        }

        order.Status = OrderStatus.Approved;
        order.ApprovedUtc = DateTime.UtcNow;
        order.WhatsAppSentUtc = null;
        await db.SaveChangesAsync();
        await emails.SendApprovedAsync(order);
        return new(true, $"Order {order.OrderNumber} approved. Now send the customer their payment details on WhatsApp.");
    }

    public async Task<WorkflowResult> RejectAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        order.Status = OrderStatus.Rejected;
        await db.SaveChangesAsync();
        await emails.SendRejectedAsync(order);
        return new(true, $"Order {order.OrderNumber} rejected.");
    }

    public async Task<WorkflowResult> ReopenAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        order.Status = OrderStatus.PendingApproval;
        await db.SaveChangesAsync();
        return new(true, $"Order {order.OrderNumber} reopened.");
    }

    public async Task<WorkflowResult> MarkPaidAsync(int orderId)
    {
        var order = await LoadAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        if (order.Status != OrderStatus.Approved)
        {
            return new(false, $"Order {order.OrderNumber} must be approved before it can be marked as paid.");
        }

        order.Status = OrderStatus.Paid;
        order.WhatsAppSentUtc = null;

        foreach (var item in order.Items)
        {
            var product = await db.Products.FindAsync(item.ProductId);
            if (product is not null)
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
            }
        }

        await db.SaveChangesAsync();
        await referrals.RecordUseAsync(order.ReferralCodeUsed);
        var referral = await referrals.GetOrCreateForOrderAsync(order);
        await emails.SendPaidAsync(order, referral);

        return new(true, $"Order {order.OrderNumber} marked as paid and stock updated. Send the customer their referral code on WhatsApp.");
    }

    public async Task<WorkflowResult> MarkShippedAsync(int orderId, string? trackingNumber = null)
    {
        var order = await LoadAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        if (order.Status != OrderStatus.Paid)
        {
            return new(false, $"Order {order.OrderNumber} must be paid before it can be shipped.");
        }

        order.Status = OrderStatus.Shipped;
        order.ShippedUtc = DateTime.UtcNow;
        order.TrackingNumber = NormalizeTracking(trackingNumber);
        order.WhatsAppSentUtc = null;
        await db.SaveChangesAsync();
        await emails.SendShippedAsync(order);
        return new(true, $"Order {order.OrderNumber} marked as shipped. Let the customer know on WhatsApp.");
    }

    public async Task<WorkflowResult> UpdateTrackingAsync(int orderId, string? trackingNumber)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        order.TrackingNumber = NormalizeTracking(trackingNumber);
        await db.SaveChangesAsync();
        return new(true, order.TrackingNumber is null ? "Tracking reference cleared." : $"Tracking reference saved: {order.TrackingNumber}.");
    }

    private static string? NormalizeTracking(string? value)
    {
        var trimmed = (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Records that the owner has sent the current stage's WhatsApp message to the customer.</summary>
    public async Task<WorkflowResult> MarkWhatsAppSentAsync(int orderId)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order is null)
        {
            return new(false, "Order not found.");
        }

        order.WhatsAppSentUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new(true, $"WhatsApp message marked as sent for Order {order.OrderNumber}.");
    }

    private Task<Order?> LoadAsync(int id) =>
        db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
}
