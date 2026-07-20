using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
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
        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Avatar must be smaller than 5MB.");

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{username}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.webp";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var image = await Image.LoadAsync(file.OpenReadStream());
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(256, 256),
            Mode = ResizeMode.Crop
        }));

        await image.SaveAsync(filePath, new WebpEncoder { Quality = 85 });

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
