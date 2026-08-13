using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ProfileService.Services;

public interface IAvatarStorageService
{
    Task<string> SaveAvatarAsync(IFormFile file, string username);
    Task<string> SaveCoverAsync(IFormFile file, string username);
    void DeleteAvatar(string? relativePath);
    bool FileExists(string? relativePath);
    string ResolvePublicUrl(string? relativePath);
}

public class AvatarStorageService : IAvatarStorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AvatarStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    // Uploads live outside the container's ephemeral wwwroot so they survive
    // restarts/redeploys. Backed by a persistent volume in deployment.
    private string MediaRoot =>
        _config["MediaStoragePath"] ?? Path.Combine(_env.WebRootPath ?? "wwwroot");

    public async Task<string> SaveAvatarAsync(IFormFile file, string username)
    {
        // Allow larger uploads — iPhone photos are often 8–12 MB.
        if (file.Length > 15 * 1024 * 1024)
            throw new InvalidOperationException("Avatar must be smaller than 15MB.");

        var uploadsDir = Path.Combine(MediaRoot, "avatars");
        Directory.CreateDirectory(uploadsDir);

        // Always store as JPEG regardless of the uploaded format (png, jpg,
        // webp, bmp, gif, tga…). ImageSharp decodes the input and re-encodes.
        var fileName = $"{username}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        Image image;
        try
        {
            image = await Image.LoadAsync(file.OpenReadStream());
        }
        catch
        {
            // Unsupported format (e.g. HEIC that wasn't converted client-side).
            throw new InvalidOperationException(
                "Unsupported image format. Please upload a JPG or PNG.");
        }

        using (image)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(256, 256),
                Mode = ResizeMode.Crop
            }));
            await image.SaveAsync(filePath, new JpegEncoder { Quality = 88 });
        }

        return $"/avatars/{fileName}";
    }

    public async Task<string> SaveCoverAsync(IFormFile file, string username)
    {
        if (file.Length > 8 * 1024 * 1024)
            throw new InvalidOperationException("Cover image must be smaller than 8MB.");

        var uploadsDir = Path.Combine(MediaRoot, "covers");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{username}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        Image image;
        try
        {
            image = await Image.LoadAsync(file.OpenReadStream());
        }
        catch
        {
            throw new InvalidOperationException(
                "Unsupported image format. Please upload a JPG or PNG.");
        }

        using (image)
        {
            // Wide banner: cap width, keep aspect, crop to a 16:6 banner.
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1200, 450),
                Mode = ResizeMode.Crop
            }));
            await image.SaveAsync(filePath, new JpegEncoder { Quality = 85 });
        }

        return $"/covers/{fileName}";
    }

    public void DeleteAvatar(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        var full = Path.Combine(MediaRoot, relativePath.TrimStart('/'));
        if (File.Exists(full))
            File.Delete(full);
    }

    public bool FileExists(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return false;
        if (relativePath.StartsWith("http")) return true; // external URL, assume valid
        var full = Path.Combine(MediaRoot, relativePath.TrimStart('/'));
        return File.Exists(full);
    }

    public string ResolvePublicUrl(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (relativePath.StartsWith("http")) return relativePath;
        var baseUrl = _config["PublicBaseUrl"] ?? "http://localhost:8011";
        return $"{baseUrl}{relativePath}";
    }
}
