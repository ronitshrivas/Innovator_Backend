namespace ResearchService.Services;

public interface IFileStorage
{
    /// <summary>Saves an uploaded file and returns its public URL.</summary>
    Task<string> SaveAsync(IFormFile file, string folder);
}

public class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public LocalFileStorage(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var targetDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(targetDir);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var baseUrl = (_config["PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/uploads/{folder}/{fileName}";
    }
}
