using System.ComponentModel.DataAnnotations;

namespace tanningTribe.Models;

public enum MessageDirection
{
    Inbound,
    Outbound
}

public class Conversation
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageUtc { get; set; } = DateTime.UtcNow;

    public int UnreadCount { get; set; }

    public List<EmailMessage> Messages { get; set; } = [];
}

public class EmailMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public MessageDirection Direction { get; set; }

    [MaxLength(200)]
    public string FromAddress { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ToAddress { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    public string? TextBody { get; set; }

    public string? HtmlBody { get; set; }

    public List<EmailAttachment> Attachments { get; set; } = [];

    /// <summary>RFC 5322 Message-ID header, used to thread replies.</summary>
    [MaxLength(300)]
    public string? MessageId { get; set; }

    [MaxLength(300)]
    public string? InReplyTo { get; set; }

    /// <summary>Resend's id for the email (inbound email_id or outbound send id).</summary>
    [MaxLength(100)]
    public string? ResendEmailId { get; set; }

    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
}

public class EmailAttachment
{
    public int Id { get; set; }

    public int EmailMessageId { get; set; }
    public EmailMessage? EmailMessage { get; set; }

    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Content-ID header (without angle brackets) for inline images referenced as cid: in the HTML body.</summary>
    [MaxLength(300)]
    public string? ContentId { get; set; }

    public bool IsInline { get; set; }

    public long Size { get; set; }

    public byte[] Data { get; set; } = [];
}
