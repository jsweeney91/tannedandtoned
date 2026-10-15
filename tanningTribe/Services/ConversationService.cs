using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tanningTribe.Data;
using tanningTribe.Models;

namespace tanningTribe.Services;

/// <summary>Stores inbound/outbound emails and groups them into threaded conversations.</summary>
public partial class ConversationService(StoreDbContext db, ResendService resend, IOptions<EmailSettings> emailOptions, ILogger<ConversationService> logger)
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    public async Task<Conversation?> RecordInboundAsync(ResendInboundEmail email)
    {
        if (!string.IsNullOrEmpty(email.EmailId) && await db.EmailMessages.AnyAsync(m => m.ResendEmailId == email.EmailId))
        {
            return null; // webhook retry - already stored
        }

        var (fromAddress, fromName) = SplitAddress(email.From);
        var subject = string.IsNullOrWhiteSpace(email.Subject) ? "(no subject)" : email.Subject.Trim();

        var conversation = await FindThreadAsync(email.InReplyTo, fromAddress, subject);
        if (conversation is null)
        {
            conversation = new Conversation
            {
                CustomerEmail = fromAddress,
                CustomerName = fromName,
                Subject = subject,
                OrderId = await MatchOrderIdAsync(fromAddress, subject, email.Text)
            };
            db.Conversations.Add(conversation);
        }
        else if (string.IsNullOrEmpty(conversation.CustomerName) && !string.IsNullOrEmpty(fromName))
        {
            conversation.CustomerName = fromName;
        }

        var message = new EmailMessage
        {
            Direction = MessageDirection.Inbound,
            FromAddress = fromAddress,
            ToAddress = email.To,
            Subject = subject,
            TextBody = email.Text,
            HtmlBody = email.Html,
            MessageId = email.MessageId,
            InReplyTo = email.InReplyTo,
            ResendEmailId = email.EmailId,
            SentUtc = email.ReceivedUtc
        };

        foreach (var info in email.Attachments)
        {
            if (info.Size > MaxAttachmentBytes)
            {
                logger.LogWarning("Skipping attachment {Name} ({Size} bytes) - too large", info.FileName, info.Size);
                continue;
            }

            var bytes = await resend.DownloadAttachmentAsync(email.EmailId, info);
            if (bytes is null)
            {
                continue;
            }

            var isInline = !string.IsNullOrEmpty(info.ContentId)
                && (email.Html?.Contains($"cid:{info.ContentId}", StringComparison.OrdinalIgnoreCase) ?? false);

            message.Attachments.Add(new EmailAttachment
            {
                FileName = info.FileName,
                ContentType = info.ContentType,
                ContentId = info.ContentId,
                IsInline = isInline,
                Size = bytes.LongLength,
                Data = bytes
            });
        }

        conversation.Messages.Add(message);
        conversation.LastMessageUtc = email.ReceivedUtc;
        conversation.UnreadCount++;

        await db.SaveChangesAsync();
        logger.LogInformation("Inbound email from {From} stored in conversation {Id}", fromAddress, conversation.Id);
        return conversation;
    }

    public async Task<WorkflowResult> ReplyAsync(int conversationId, string body)
    {
        var conversation = await db.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation is null)
        {
            return new(false, "Conversation not found.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new(false, "Reply cannot be empty.");
        }

        var last = conversation.Messages.OrderByDescending(m => m.SentUtc).FirstOrDefault();
        var subject = conversation.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? conversation.Subject : $"Re: {conversation.Subject}";
        var references = conversation.Messages.Where(m => m.MessageId is not null).OrderBy(m => m.SentUtc).Select(m => m.MessageId!);

        var result = await resend.SendAsync(conversation.CustomerEmail, subject, body.Trim(), null, last?.MessageId, references);
        if (!result.Success)
        {
            return new(false, result.Message);
        }

        conversation.Messages.Add(new EmailMessage
        {
            Direction = MessageDirection.Outbound,
            FromAddress = emailOptions.Value.FromAddress,
            ToAddress = conversation.CustomerEmail,
            Subject = subject,
            TextBody = body.Trim(),
            InReplyTo = last?.MessageId,
            ResendEmailId = result.EmailId,
            SentUtc = DateTime.UtcNow
        });
        conversation.LastMessageUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new(true, "Reply sent.");
    }

    public async Task MarkReadAsync(Conversation conversation)
    {
        if (conversation.UnreadCount > 0)
        {
            conversation.UnreadCount = 0;
            await db.SaveChangesAsync();
        }
    }

    private async Task<Conversation?> FindThreadAsync(string? inReplyTo, string fromAddress, string subject)
    {
        if (!string.IsNullOrWhiteSpace(inReplyTo))
        {
            var parent = await db.EmailMessages.Include(m => m.Conversation).FirstOrDefaultAsync(m => m.MessageId == inReplyTo);
            if (parent?.Conversation is not null)
            {
                return parent.Conversation;
            }
        }

        var normalized = NormalizeSubject(subject);
        var since = DateTime.UtcNow.AddDays(-30);
        var candidates = await db.Conversations
            .Where(c => c.CustomerEmail == fromAddress && c.LastMessageUtc >= since)
            .OrderByDescending(c => c.LastMessageUtc)
            .Take(20)
            .ToListAsync();

        return candidates.FirstOrDefault(c => NormalizeSubject(c.Subject) == normalized);
    }

    private async Task<int?> MatchOrderIdAsync(string fromAddress, string subject, string? text)
    {
        var match = OrderNumberRegex().Match(subject + " " + (text ?? string.Empty));
        if (match.Success)
        {
            var number = match.Value.ToUpperInvariant();
            var byNumber = await db.Orders.Where(o => o.OrderNumber == number).Select(o => (int?)o.Id).FirstOrDefaultAsync();
            if (byNumber is not null)
            {
                return byNumber;
            }
        }

        return await db.Orders
            .Where(o => o.CustomerEmail == fromAddress)
            .OrderByDescending(o => o.CreatedUtc)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync();
    }

    internal static string NormalizeSubject(string subject)
    {
        var s = subject.Trim();
        while (true)
        {
            var m = ReplyPrefixRegex().Match(s);
            if (!m.Success)
            {
                break;
            }

            s = s[m.Length..].TrimStart();
        }

        return s.ToLowerInvariant();
    }

    internal static (string Address, string? Name) SplitAddress(string raw)
    {
        try
        {
            var parsed = new MailAddress(raw);
            return (parsed.Address.ToLowerInvariant(), string.IsNullOrWhiteSpace(parsed.DisplayName) ? null : parsed.DisplayName);
        }
        catch
        {
            return (raw.Trim().ToLowerInvariant(), null);
        }
    }

    [GeneratedRegex(@"^(re|fw|fwd)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefixRegex();

    [GeneratedRegex(@"\bTT-[A-Z0-9]{4,10}\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrderNumberRegex();
}
