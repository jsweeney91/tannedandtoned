using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace tanningTribe.Services;

public record ResendSendResult(bool Success, string Message, string? EmailId = null);

public record ResendAttachmentInfo(string Id, string FileName, string ContentType, string? ContentId, long Size, string? DownloadUrl);

public record ResendInboundEmail(
    string EmailId,
    string From,
    string To,
    string Subject,
    string? Text,
    string? Html,
    string? MessageId,
    string? InReplyTo,
    DateTime ReceivedUtc,
    IReadOnlyList<ResendAttachmentInfo> Attachments);

/// <summary>Thin client for the Resend HTTP API (send + fetch received emails) and Svix webhook verification.</summary>
public class ResendService(HttpClient http, IOptions<EmailSettings> options, ILogger<ResendService> logger)
{
    private readonly EmailSettings _settings = options.Value;

    public bool IsConfigured => _settings.IsConfigured;

    public Task<ResendSendResult> SendAsync(string to, string subject, string textBody, string? htmlBody = null, string? inReplyToMessageId = null, IEnumerable<string>? references = null) =>
        SendAsync([to], subject, textBody, htmlBody, inReplyToMessageId, references);

    public async Task<ResendSendResult> SendAsync(string[] to, string subject, string textBody, string? htmlBody = null, string? inReplyToMessageId = null, IEnumerable<string>? references = null)
    {
        if (!IsConfigured)
        {
            return new(false, "Email is not configured. Set Email:ApiKey and Email:FromAddress.");
        }

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(inReplyToMessageId))
        {
            headers["In-Reply-To"] = inReplyToMessageId;
            var refs = (references ?? []).Append(inReplyToMessageId).Distinct();
            headers["References"] = string.Join(" ", refs);
        }

        var payload = new
        {
            from = $"{_settings.FromName} <{_settings.FromAddress}>",
            to,
            reply_to = _settings.FromAddress,
            subject,
            text = textBody,
            html = string.IsNullOrWhiteSpace(htmlBody) ? null : htmlBody,
            headers = headers.Count > 0 ? headers : null
        };

        try
        {
            using var request = BuildRequest(HttpMethod.Post, "emails");
            request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Resend send failed ({Status}): {Body}", (int)response.StatusCode, body);
                return new(false, $"Resend rejected the email ({(int)response.StatusCode}).");
            }

            using var json = JsonDocument.Parse(body);
            var id = json.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            return new(true, "Email sent.", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend send threw");
            return new(false, $"Could not reach Resend: {ex.Message}");
        }
    }

    public async Task<ResendInboundEmail?> GetReceivedAsync(string emailId)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, $"emails/receiving/{Uri.EscapeDataString(emailId)}");
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Resend fetch received email failed ({Status}): {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var json = JsonDocument.Parse(body);
            return ParseInbound(json.RootElement, emailId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend fetch received email threw");
            return null;
        }
    }

    public static ResendInboundEmail ParseInbound(JsonElement data, string? fallbackId = null)
    {
        string? Str(string name) => data.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        string FirstOf(string name)
        {
            if (!data.TryGetProperty(name, out var p))
            {
                return string.Empty;
            }

            return p.ValueKind switch
            {
                JsonValueKind.Array when p.GetArrayLength() > 0 => p[0].GetString() ?? string.Empty,
                JsonValueKind.String => p.GetString() ?? string.Empty,
                _ => string.Empty
            };
        }

        string? Header(string name)
        {
            if (!data.TryGetProperty("headers", out var h))
            {
                return null;
            }

            if (h.ValueKind == JsonValueKind.Object && h.TryGetProperty(name, out var v))
            {
                return v.GetString();
            }

            if (h.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in h.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var n) && string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase)
                        && item.TryGetProperty("value", out var val))
                    {
                        return val.GetString();
                    }
                }
            }

            return null;
        }

        var created = Str("created_at");
        var attachments = new List<ResendAttachmentInfo>();
        if (data.TryGetProperty("attachments", out var atts) && atts.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in atts.EnumerateArray())
            {
                string? AStr(string name) => a.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                long ASize() => a.TryGetProperty("size", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0;

                var id = AStr("id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                attachments.Add(new ResendAttachmentInfo(
                    id,
                    AStr("filename") ?? AStr("name") ?? "attachment",
                    AStr("content_type") ?? AStr("contentType") ?? "application/octet-stream",
                    AStr("content_id")?.Trim('<', '>'),
                    ASize(),
                    AStr("download_url") ?? AStr("url")));
            }
        }

        return new ResendInboundEmail(
            EmailId: Str("email_id") ?? Str("id") ?? fallbackId ?? string.Empty,
            From: FirstOf("from"),
            To: FirstOf("to"),
            Subject: Str("subject") ?? string.Empty,
            Text: Str("text"),
            Html: Str("html"),
            MessageId: Str("message_id") ?? Header("Message-ID") ?? Header("Message-Id"),
            InReplyTo: Str("in_reply_to") ?? Header("In-Reply-To"),
            ReceivedUtc: DateTime.TryParse(created, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.UtcNow,
            Attachments: attachments);
    }

    /// <summary>Downloads a received attachment's bytes. Uses the pre-signed download_url when present, otherwise the attachments endpoint.</summary>
    public async Task<byte[]?> DownloadAttachmentAsync(string emailId, ResendAttachmentInfo attachment)
    {
        try
        {
            var url = attachment.DownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                using var meta = BuildRequest(HttpMethod.Get, $"emails/receiving/{Uri.EscapeDataString(emailId)}/attachments/{Uri.EscapeDataString(attachment.Id)}");
                using var metaResponse = await http.SendAsync(meta);
                if (!metaResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning("Resend attachment lookup failed ({Status}): {Body}", (int)metaResponse.StatusCode, await metaResponse.Content.ReadAsStringAsync());
                    return null;
                }

                var contentType = metaResponse.Content.Headers.ContentType?.MediaType;
                if (contentType is not null && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    return await metaResponse.Content.ReadAsByteArrayAsync();
                }

                using var json = JsonDocument.Parse(await metaResponse.Content.ReadAsStringAsync());
                url = json.RootElement.TryGetProperty("download_url", out var d) ? d.GetString() : null;
                if (string.IsNullOrEmpty(url))
                {
                    return null;
                }
            }

            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Resend attachment download failed ({Status})", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend attachment download threw");
            return null;
        }
    }

    /// <summary>Verifies a Svix-signed webhook (Resend uses Svix). Returns false if the secret is missing.</summary>
    public bool VerifyWebhook(IHeaderDictionary headers, string payload)
    {
        var secret = _settings.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || secret.Contains("SET_IN_USER_SECRETS"))
        {
            logger.LogWarning("Email:WebhookSecret is not set; rejecting webhook.");
            return false;
        }

        var msgId = headers["svix-id"].ToString();
        var timestamp = headers["svix-timestamp"].ToString();
        var signatures = headers["svix-signature"].ToString();
        if (string.IsNullOrEmpty(msgId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatures))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
        {
            return false;
        }

        var keyPart = secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret;
        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyPart);
        }
        catch (FormatException)
        {
            return false;
        }

        var toSign = Encoding.UTF8.GetBytes($"{msgId}.{timestamp}.{payload}");
        var expected = Convert.ToBase64String(HMACSHA256.HashData(key, toSign));

        foreach (var sig in signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = sig.Split(',', 2);
            if (parts.Length == 2 && parts[0] == "v1"
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(expected)))
            {
                return true;
            }
        }

        return false;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string relative)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri("https://api.resend.com/"), relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
