using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Services;

/// <summary>Sends branded HTML order emails (with product images) via Resend and records them as conversation messages.</summary>
public class OrderEmailService(
    StoreDbContext db,
    ResendService resend,
    WhatsAppService messages,
    IOptions<EmailSettings> emailOptions,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OrderEmailService> logger)
{
    private readonly EmailSettings _email = emailOptions.Value;

    public Task SendReceivedAsync(Order order) =>
        SendAsync(order, $"Order {order.OrderNumber} received – Tanned & Toned", messages.BuildReceivedMessage(order), showItems: true);

    public Task SendApprovedAsync(Order order) =>
        SendAsync(order, $"Order {order.OrderNumber} approved – payment details", messages.BuildApprovalMessage(order), showItems: true);

    public Task SendPaidAsync(Order order, ReferralCode? referral) =>
        SendAsync(order, $"Payment received for order {order.OrderNumber}", messages.BuildPaidMessage(order, referral), showItems: false);

    public Task SendShippedAsync(Order order) =>
        SendAsync(order, $"Order {order.OrderNumber} has shipped", messages.BuildShippedMessage(order), showItems: true);

    public Task SendRejectedAsync(Order order) =>
        SendAsync(order, $"Order {order.OrderNumber} – update", messages.BuildRejectedMessage(order), showItems: false);

    /// <summary>Notifies the owner that a new order has been placed.</summary>
    public async Task NotifyOwnerNewOrderAsync(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"New order {order.OrderNumber} from {order.CustomerName}.");
        sb.AppendLine();
        foreach (var item in order.Items)
        {
            sb.AppendLine($"• {item.ProductName} × {item.Quantity} – {(item.UnitPrice * item.Quantity):C}");
        }
        if (order.DiscountAmount > 0)
        {
            sb.AppendLine($"• Referral discount ({order.ReferralCodeUsed}) – -{order.DiscountAmount:C}");
        }
        sb.AppendLine($"Total: {order.Total:C}");
        sb.AppendLine();
        sb.AppendLine($"Email: {order.CustomerEmail}");
        sb.AppendLine($"WhatsApp: {order.WhatsAppNumber}");
        sb.AppendLine($"Address: {order.ShippingAddress.Replace("\r", "").Replace("\n", ", ")}");
        if (!string.IsNullOrWhiteSpace(order.CustomerNotes))
        {
            sb.AppendLine($"Notes: {order.CustomerNotes}");
        }
        sb.AppendLine();
        sb.AppendLine($"Review it: {ResolveBaseUrl()}/Admin/Orders/Details/{order.Id}");

        await NotifyOwnerAsync($"🛍️ New order {order.OrderNumber} – {order.CustomerName} ({order.Total:C})", sb.ToString());
    }

    /// <summary>Notifies the owner that a customer email has arrived.</summary>
    public async Task NotifyOwnerNewMessageAsync(Conversation conversation, EmailMessage message)
    {
        var preview = (message.TextBody ?? string.Empty).Trim();
        if (preview.Length > 1500)
        {
            preview = preview[..1500] + "…";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"From: {conversation.CustomerName ?? message.FromAddress} <{message.FromAddress}>");
        sb.AppendLine($"Subject: {message.Subject}");
        if (conversation.Order is not null)
        {
            sb.AppendLine($"Order: {conversation.Order.OrderNumber}");
        }
        if (message.Attachments.Count > 0)
        {
            sb.AppendLine($"Attachments: {string.Join(", ", message.Attachments.Select(a => a.FileName))}");
        }
        sb.AppendLine();
        sb.AppendLine(preview);
        sb.AppendLine();
        sb.AppendLine($"Reply here: {ResolveBaseUrl()}/Admin/Messages/Thread/{conversation.Id}");

        await NotifyOwnerAsync($"✉️ New message from {conversation.CustomerName ?? message.FromAddress}: {message.Subject}", sb.ToString());
    }

    private async Task NotifyOwnerAsync(string subject, string body)
    {
        if (!_email.IsConfigured || string.IsNullOrWhiteSpace(_email.OwnerNotifyAddress))
        {
            return;
        }

        try
        {
            var result = await resend.SendAsync(_email.OwnerNotifyAddress, subject, body);
            if (!result.Success)
            {
                logger.LogWarning("Owner notification '{Subject}' failed: {Message}", subject, result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Owner notification '{Subject}' threw", subject);
        }
    }

    private async Task SendAsync(Order order, string subject, string textBody, bool showItems)
    {
        if (!_email.IsConfigured || !_email.SendOrderEmails || string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return;
        }

        try
        {
            var html = await BuildHtmlAsync(order, textBody, showItems);
            var plain = StripWhatsAppMarkup(textBody);

            var conversation = await db.Conversations
                .Include(c => c.Messages)
                .Where(c => c.OrderId == order.Id)
                .OrderByDescending(c => c.LastMessageUtc)
                .FirstOrDefaultAsync();

            var last = conversation?.Messages.OrderByDescending(m => m.SentUtc).FirstOrDefault();
            var references = conversation?.Messages.Where(m => m.MessageId is not null).OrderBy(m => m.SentUtc).Select(m => m.MessageId!);

            var result = await resend.SendAsync(order.CustomerEmail, subject, plain, html, last?.MessageId, references);
            if (!result.Success)
            {
                logger.LogWarning("Order email '{Subject}' to {Email} failed: {Message}", subject, order.CustomerEmail, result.Message);
                return;
            }

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    CustomerEmail = order.CustomerEmail.ToLowerInvariant(),
                    CustomerName = order.CustomerName,
                    Subject = subject,
                    OrderId = order.Id
                };
                db.Conversations.Add(conversation);
            }

            conversation.Messages.Add(new EmailMessage
            {
                Direction = MessageDirection.Outbound,
                FromAddress = _email.FromAddress,
                ToAddress = order.CustomerEmail,
                Subject = subject,
                TextBody = plain,
                HtmlBody = html,
                InReplyTo = last?.MessageId,
                ResendEmailId = result.EmailId,
                SentUtc = DateTime.UtcNow
            });
            conversation.LastMessageUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed sending order email '{Subject}' to {Email}", subject, order.CustomerEmail);
        }
    }

    private async Task<string> BuildHtmlAsync(Order order, string textBody, bool showItems)
    {
        var baseUrl = ResolveBaseUrl();
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var images = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ImageUrl);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><body style=\"margin:0;padding:0;background:#faf6f0;font-family:Helvetica,Arial,sans-serif;color:#2b2b2b;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\" style=\"padding:24px 12px;\">");
        sb.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border-radius:16px;overflow:hidden;\">");
        sb.Append("<tr><td style=\"background:#c9a24d;padding:20px 28px;color:#fff;font-size:22px;font-weight:bold;letter-spacing:1px;\">Tanned &amp; Toned</td></tr>");
        sb.Append("<tr><td style=\"padding:28px;font-size:15px;line-height:1.6;white-space:pre-wrap;\">");
        sb.Append(ToHtml(textBody));
        sb.Append("</td></tr>");

        if (showItems && order.Items.Count > 0)
        {
            sb.Append("<tr><td style=\"padding:0 28px 8px;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-top:1px solid #eee;\">");
            foreach (var item in order.Items)
            {
                var img = images.TryGetValue(item.ProductId, out var url) ? Absolute(baseUrl, url) : null;
                sb.Append("<tr><td style=\"padding:12px 0;border-bottom:1px solid #eee;\" width=\"72\">");
                if (img is not null)
                {
                    sb.Append($"<img src=\"{WebUtility.HtmlEncode(img)}\" alt=\"{WebUtility.HtmlEncode(item.ProductName)}\" width=\"64\" height=\"64\" style=\"display:block;width:64px;height:64px;object-fit:cover;border-radius:10px;\" />");
                }
                sb.Append("</td>");
                sb.Append($"<td style=\"padding:12px 8px;border-bottom:1px solid #eee;font-size:14px;\">{WebUtility.HtmlEncode(item.ProductName)}<br/><span style=\"color:#888;\">&times; {item.Quantity}</span></td>");
                sb.Append($"<td align=\"right\" style=\"padding:12px 0;border-bottom:1px solid #eee;font-size:14px;white-space:nowrap;\">{(item.UnitPrice * item.Quantity):C}</td></tr>");
            }

            if (order.DiscountAmount > 0)
            {
                sb.Append($"<tr><td colspan=\"2\" style=\"padding:8px 0;color:#2e7d32;font-size:14px;\">Referral discount ({WebUtility.HtmlEncode(order.ReferralCodeUsed)})</td><td align=\"right\" style=\"padding:8px 0;color:#2e7d32;font-size:14px;\">-{order.DiscountAmount:C}</td></tr>");
            }

            sb.Append($"<tr><td colspan=\"2\" style=\"padding:10px 0;font-weight:bold;\">Total</td><td align=\"right\" style=\"padding:10px 0;font-weight:bold;\">{order.Total:C}</td></tr>");
            sb.Append("</table></td></tr>");
        }

        sb.Append("<tr><td style=\"padding:16px 28px 24px;font-size:12px;color:#999;\">Reply to this email if you have any questions.</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_email.PublicBaseUrl))
        {
            return _email.PublicBaseUrl.TrimEnd('/');
        }

        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? string.Empty : $"{request.Scheme}://{request.Host}";
    }

    private static string? Absolute(string baseUrl, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return string.IsNullOrEmpty(baseUrl) ? null : baseUrl + (url.StartsWith('/') ? url : "/" + url);
    }

    /// <summary>Converts the WhatsApp-style message (*bold*, newlines, bare URLs) to simple HTML.</summary>
    private static string ToHtml(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = System.Text.RegularExpressions.Regex.Replace(encoded, @"\*([^*\n]+)\*", "<strong>$1</strong>");
        encoded = System.Text.RegularExpressions.Regex.Replace(encoded, @"(https?://[^\s<]+)", "<a href=\"$1\" style=\"color:#c9a24d;\">$1</a>");
        return encoded;
    }

    private static string StripWhatsAppMarkup(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\*([^*\n]+)\*", "$1");
}
