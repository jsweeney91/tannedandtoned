namespace tanningTribe.Services;

public class ImageService(IWebHostEnvironment env)
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxBytes = 5 * 1024 * 1024;

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

        var folder = Path.Combine(env.WebRootPath, "images", "products");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(folder, fileName);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream);

        return $"/images/products/{fileName}";
    }

    public void DeleteIfLocal(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("/images/products/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = Path.Combine(env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
