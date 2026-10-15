namespace tanningTribe.Services;

public class RoyalMailSettings
{
    /// <summary>Click &amp; Drop API key (Click &amp; Drop → Settings → Integrations → Click &amp; Drop API).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.parcel.royalmail.com/api/v1/";

    /// <summary>Default Click &amp; Drop service code, e.g. "TPS48" (Tracked 48), "TPN24" (Tracked 24), "CRL1" (1st Class).</summary>
    public string DefaultServiceCode { get; set; } = "TPS48";

    public int DefaultWeightGrams { get; set; } = 500;

    /// <summary>"parcel", "largeLetter", "letter" or "mediumParcel".</summary>
    public string PackageFormat { get; set; } = "parcel";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.Contains("SET_IN_USER_SECRETS");
}
