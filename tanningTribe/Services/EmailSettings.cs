namespace tanningTribe.Services;

public class EmailSettings
{
    /// <summary>Resend API key (starts with "re_").</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Svix signing secret for the Resend webhook endpoint (starts with "whsec_").</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public string FromName { get; set; } = "Tanned & Toned";

    /// <summary>Address on your verified Resend domain, e.g. "hello@yourdomain.com". Also used as Reply-To so replies come back via the webhook.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Public https origin of the site, e.g. "https://tannedandtoned.co.uk". Used to make product image URLs absolute in emails.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Send order status emails to customers automatically (received / approved / paid / shipped).</summary>
    public bool SendOrderEmails { get; set; } = true;

    /// <summary>Owner inbox(es), comma or semicolon separated. Each gets a notification for every new order and inbound customer email.</summary>
    public string OwnerNotifyAddress { get; set; } = string.Empty;

    public string[] OwnerNotifyAddresses =>
        OwnerNotifyAddress.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Contains("SET_IN_USER_SECRETS") && !string.IsNullOrWhiteSpace(FromAddress);
}
