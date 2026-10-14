using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public enum OrderStatus
{
    PendingApproval,
    Approved,
    Paid,
    Shipped,
    Rejected
}

public class Order
{
    public int Id { get; set; }

    /// <summary>Customer-facing order number, also used as the bank transfer reference, e.g. "TT-7K3Q9M".</summary>
    [Required, MaxLength(20)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [MaxLength(30)]
    public string WhatsAppNumber { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? CustomerNotes { get; set; }

    public DateTime? ApprovedUtc { get; set; }

    public DateTime? WhatsAppSentUtc { get; set; }

    public DateTime? ShippedUtc { get; set; }

    /// <summary>Royal Mail tracking reference, e.g. "AB123456789GB".</summary>
    [MaxLength(40)]
    public string? TrackingNumber { get; set; }

    [MaxLength(100)]
    public string? WhatsAppMessageSid { get; set; }

    [MaxLength(500)]
    public string? WhatsAppError { get; set; }

    public decimal Total { get; set; }

    public decimal DiscountAmount { get; set; }

    [MaxLength(20)]
    public string? ReferralCodeUsed { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingApproval;

    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [MaxLength(120)]
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
