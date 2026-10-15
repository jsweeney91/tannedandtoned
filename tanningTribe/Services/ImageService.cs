using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace tanningTribe.Services;

public class ImageService(IWebHostEnvironment env, IOptions<BlobStorageSettings> blobOptions, ILogger<ImageService> logger)
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly BlobStorageSettings _blob = blobOptions.Value;
    private IAmazonS3? _s3;

    public bool UsesBlobStorage => _blob.IsConfigured;

    public async Task<string> SaveProductImageAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException("Only JPG, PNG, WEBP or GIF images are allowed.");
        }

        if (file.Length > MaxBytes)
        {
            throw new InvalidOperationException("Image must be 5 MB or smaller.");
        }

        var fileName = $"{Guid.NewGuid():N}{ext}";

        if (UsesBlobStorage)
        {
            var key = $"{_blob.Prefix.Trim('/')}/{fileName}";
            await using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = _blob.Bucket,
                Key = key,
                InputStream = stream,
                ContentType = ContentTypeFor(ext),
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };
            request.Headers.CacheControl = "public, max-age=31536000, immutable";
            await Client().PutObjectAsync(request);
            return $"{_blob.PublicBaseUrl.TrimEnd('/')}/{key}";
        }

        var folder = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        await using var local = File.Create(path);
        await file.CopyToAsync(local);
        return $"/images/products/{fileName}";
    }

    /// <summary>Deletes the image if it was uploaded by us (local wwwroot or our blob bucket). External URLs are left alone.</summary>
    public async Task DeleteIfOwnedAsync(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return;
        }

        if (imageUrl.StartsWith("/images/products/", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.Combine(env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return;
        }

        if (UsesBlobStorage)
        {
            var baseUrl = _blob.PublicBaseUrl.TrimEnd('/') + "/";
            if (imageUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                var key = imageUrl[baseUrl.Length..];
                try
                {
                    await Client().DeleteObjectAsync(_blob.Bucket, key);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete blob {Key}", key);
                }
            }
        }
    }

    private IAmazonS3 Client()
    {
        if (_s3 is not null)
        {
            return _s3;
        }

        var config = new AmazonS3Config
        {
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (!string.IsNullOrWhiteSpace(_blob.ServiceUrl))
        {
            config.ServiceURL = _blob.ServiceUrl;
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(_blob.Region) ? "auto" : _blob.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_blob.Region);
        }

        _s3 = new AmazonS3Client(new BasicAWSCredentials(_blob.AccessKey, _blob.SecretKey), config);
        return _s3;
    }

    private static string ContentTypeFor(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
