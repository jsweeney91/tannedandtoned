using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using tanningTribe.Models;

namespace tanningTribe.Services;

/// <summary>Builds WhatsApp messages and wa.me links; the owner sends them from their own phone.</summary>
public partial class WhatsAppService(
    IOptions<WhatsAppSettings> whatsAppOptions,
    IOptions<PaymentSettings> paymentOptions)
{
    private readonly WhatsAppSettings _wa = whatsAppOptions.Value;
    private readonly PaymentSettings _pay = paymentOptions.Value;

    /// <summary>Builds a https://wa.me link that opens a chat with the customer and pre-fills the message.</summary>
    public string BuildLink(string toNumber, string body)
    {
        var to = NormalizeNumber(toNumber).TrimStart('+');
        return $"https://wa.me/{to}?text={Uri.EscapeDataString(body)}";
    }

    public string NormalizeNumber(string raw)
    {
        var digits = DigitsOnly().Replace(raw ?? string.Empty, string.Empty);
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (raw!.TrimStart().StartsWith('+'))
        {
            return "+" + digits;
        }

        if (digits.StartsWith("00"))
        {
            return "+" + digits[2..];
        }

        if (digits.StartsWith('0'))
        {
            return "+" + _wa.DefaultCountryCode + digits[1..];
        }

        if (digits.StartsWith(_wa.DefaultCountryCode))
        {
            return "+" + digits;
        }

        return "+" + _wa.DefaultCountryCode + digits;
    }

    /// <summary>Sent while an order is awaiting approval. Deliberately contains no payment details.</summary>
    public string BuildReceivedMessage(Order order)
    {
        return $"Hi {FirstName(order.CustomerName)}! 🌴 Thanks for your Tanned & Toned order *{order.OrderNumber}*. We're just reviewing it and will message you shortly with payment details once it's approved. ✨\n\n– Tanned & Toned by Tan Tribe";
    }

    public string BuildRejectedMessage(Order order)
    {
        return $"Hi {FirstName(order.CustomerName)}, unfortunately we're unable to fulfil your Tanned & Toned order *{order.OrderNumber}* at this time. No payment has been taken. If you have any questions just reply to this message.\n\n– Tanned & Toned by Tan Tribe";
    }

    public string BuildApprovalMessage(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {FirstName(order.CustomerName)}! 🌴✨");
        sb.AppendLine();
        sb.AppendLine($"Great news – your Tanned & Toned order *{order.OrderNumber}* has been approved.");
        sb.AppendLine();
        sb.AppendLine("*Your order:*");
        foreach (var item in order.Items)
        {
            sb.AppendLine($"• {item.ProductName} × {item.Quantity} – {(item.UnitPrice * item.Quantity):C}");
        }

        if (order.DiscountAmount > 0)
        {
            sb.AppendLine($"• Referral discount ({order.ReferralCodeUsed}) – -{order.DiscountAmount:C}");
        }

        if (order.PostageAmount > 0)
        {
            sb.AppendLine($"• Postage – {order.PostageAmount:C}");
        }

        sb.AppendLine();
        sb.AppendLine($"*Total to pay: {order.Total:C}*");
        sb.AppendLine();
        sb.AppendLine("*Payment details (bank transfer):*");
        sb.AppendLine($"Name: {_pay.AccountName}");
        sb.AppendLine($"Sort code: {_pay.SortCode}");
        sb.AppendLine($"Account no: {_pay.AccountNumber}");
        if (!string.IsNullOrWhiteSpace(_pay.Iban))
        {
            sb.AppendLine($"IBAN: {_pay.Iban}");
        }
        sb.AppendLine($"Reference: *{order.OrderNumber}*");

        if (!string.IsNullOrWhiteSpace(_pay.PayPalEmail))
        {
            sb.AppendLine();
            sb.AppendLine($"Or PayPal (friends & family): {_pay.PayPalEmail}");
        }

        if (!string.IsNullOrWhiteSpace(_pay.ExtraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine(_pay.ExtraInstructions);
        }

        sb.AppendLine();
        sb.AppendLine($"Please pay within {_pay.PaymentDueHours} hours and reply to this message with a screenshot so we can get your order on its way. 💛");
        sb.AppendLine();
        sb.AppendLine("Tan smarter, not harder!");
        sb.Append("– Tanned & Toned by Tan Tribe");
        return sb.ToString();
    }

    public string BuildPaidMessage(Order order, ReferralCode? referral)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Thanks {FirstName(order.CustomerName)}! 💛");
        sb.AppendLine();
        sb.AppendLine($"We've received your payment for order *{order.OrderNumber}* and it's being packed now.");
        if (referral is not null)
        {
            sb.AppendLine();
            sb.AppendLine(referral.OwnerOrderId == order.Id
                ? $"Here's your personal Friends & Family code: *{referral.Code}*"
                : $"Don't forget your personal Friends & Family code: *{referral.Code}*");
            sb.AppendLine($"Share it with friends – anyone who uses it gets {referral.DiscountPercent}% off their order at tannedandtoned.co.uk.");
        }
        sb.AppendLine();
        sb.Append("– Tanned & Toned by Tan Tribe");
        return sb.ToString();
    }

    public static string? TrackingUrl(Order order) =>
        string.IsNullOrWhiteSpace(order.TrackingNumber) ? null : $"https://www.royalmail.com/track-your-item#/tracking-results/{order.TrackingNumber}";

    public string BuildShippedMessage(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {FirstName(order.CustomerName)}! 📦 Your Tanned & Toned order *{order.OrderNumber}* has been shipped with Royal Mail and is on its way to you.");
        if (TrackingUrl(order) is { } url)
        {
            sb.AppendLine();
            sb.AppendLine($"Tracking reference: *{order.TrackingNumber}*");
            sb.AppendLine($"Track it here: {url}");
        }
        sb.AppendLine();
        sb.AppendLine("Enjoy your glow! ✨");
        sb.Append("– Tanned & Toned by Tan Tribe");
        return sb.ToString();
    }

    private static string FirstName(string fullName)
    {
        var first = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "there" : first;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
