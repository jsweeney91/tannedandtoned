namespace tanningTribe.Services;

public class WhatsAppSettings
{
    /// <summary>Default country calling code applied when a customer enters a local number, e.g. "44".</summary>
    public string DefaultCountryCode { get; set; } = "44";
}

public class PaymentSettings
{
    public string AccountName { get; set; } = string.Empty;
    public string SortCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Iban { get; set; }
    public string? PayPalEmail { get; set; }
    public string? ExtraInstructions { get; set; }
    public int PaymentDueHours { get; set; } = 48;
}
