namespace tanningTribe.Services;

/// <summary>S3-compatible blob storage (Cloudflare R2, AWS S3, Backblaze B2, MinIO...). Falls back to local wwwroot when not configured.</summary>
public class BlobStorageSettings
{
    /// <summary>S3 endpoint, e.g. "https://&lt;accountid&gt;.r2.cloudflarestorage.com". Leave empty for AWS S3.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "auto";

    public string Bucket { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Public base URL where objects are served, e.g. "https://images.yourdomain.com" or the R2.dev URL.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string Prefix { get; set; } = "products";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKey) && !AccessKey.Contains("SET_IN_USER_SECRETS")
        && !string.IsNullOrWhiteSpace(SecretKey) && !SecretKey.Contains("SET_IN_USER_SECRETS")
        && !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
