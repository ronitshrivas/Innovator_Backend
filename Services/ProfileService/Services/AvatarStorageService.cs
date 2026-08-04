using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ProfileService.Services;

public interface IAvatarStorageService
{
    Task<string> SaveAvatarAsync(IFormFile file, string username);
    void DeleteAvatar(string? relativePath);
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

    public async Task<string> SaveAvatarAsync(IFormFile file, string username)
    {
        // Allow larger uploads — iPhone photos are often 8–12 MB.
        if (file.Length > 15 * 1024 * 1024)
            throw new InvalidOperationException("Avatar must be smaller than 15MB.");

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "avatars");
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

    public void DeleteAvatar(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        var full = Path.Combine(_env.WebRootPath ?? "wwwroot", relativePath.TrimStart('/'));
        if (File.Exists(full))
            File.Delete(full);
    }

    public string ResolvePublicUrl(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (relativePath.StartsWith("http")) return relativePath;
        var baseUrl = _config["PublicBaseUrl"] ?? "http://localhost:8011";
        return $"{baseUrl}{relativePath}";
    }
}
