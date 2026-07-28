namespace FeedService.Services;

public interface IMediaStorageService
{
    Task<(string filePath, string mediaType, string? thumbnail)> SaveMediaAsync(
        IFormFile file, string uploaderUsername);
    void DeleteMedia(string? filePath);
    string ResolvePublicUrl(string? relativePath);
}

public class MediaStorageService : IMediaStorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public MediaStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public async Task<(string filePath, string mediaType, string? thumbnail)> SaveMediaAsync(
        IFormFile file, string uploaderUsername)
    {
        if (file.Length > 100 * 1024 * 1024)
            throw new InvalidOperationException("File must be under 100MB.");

        var ext = Path.GetExtension(file.FileName).ToLower();
        var mediaType = IsVideo(ext) ? "video" : "image";
        var folder = Path.Combine(_env.WebRootPath ?? "wwwroot", "media",
            DateTime.UtcNow.ToString("yyyy/MM"));
        Directory.CreateDirectory(folder);

        var fileName = $"{uploaderUsername}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        var relativePath = $"/media/{DateTime.UtcNow:yyyy/MM}/{fileName}";
        return (relativePath, mediaType, null);
    }

    public void DeleteMedia(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var full = Path.Combine(_env.WebRootPath ?? "wwwroot", filePath.TrimStart('/'));
        if (File.Exists(full)) File.Delete(full);
    }

    public string ResolvePublicUrl(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (relativePath.StartsWith("http")) return relativePath;

        // Everything is served under /media/. Migrated rows store paths like
        // "post_media/x.jpg"; new uploads already produce "/media/...". Normalise
        // both to a single "/media/..." path and prefix the public base.
        var clean = relativePath.TrimStart('/');
        if (!clean.StartsWith("media/"))
            clean = $"media/{clean}";

        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8012").TrimEnd('/');
        return $"{baseUrl}/{clean}";
    }

    private static bool IsVideo(string ext) =>
        ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm";
}
