namespace tanningTribe.Services;

public class RoyalMailSettings
{
    /// <summary>Click &amp; Drop API key (Click &amp; Drop → Settings → Integrations → Click &amp; Drop API).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.parcel.royalmail.com/api/v1/";

    /// <summary>Default Click &amp; Drop service code, e.g. "TOLP48" (Tracked 48), "TOLP24" (Tracked 24).</summary>
    public string DefaultServiceCode { get; set; } = "TOLP48";

    public int DefaultWeightGrams { get; set; } = 500;

    /// <summary>"letter", "largeLetter", "smallParcel", "mediumParcel" or "parcel".</summary>
    public string PackageFormat { get; set; } = "smallParcel";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Contains("SET_IN_USER_SECRETS");
}
