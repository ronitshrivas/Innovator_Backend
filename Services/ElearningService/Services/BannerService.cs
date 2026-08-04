using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface IBannerService
{
    Task<ApiResponse<List<BannerDto>>> GetActiveAsync();
    Task<ApiResponse<List<BannerDto>>> GetAllAsync();
    Task<ApiResponse<BannerDto>> CreateAsync(CreateBannerRequest request);
    Task<ApiResponse<BannerDto>> UpdateAsync(Guid id, UpdateBannerRequest request);
    Task<ApiResponse<BannerDto>> SetImageAsync(Guid id, IFormFile file);
    Task<ApiResponse<bool>> DeleteAsync(Guid id);
}

public class BannerService : IBannerService
{
    private readonly ElearningDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public BannerService(ElearningDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _env = env;
    }

    public async Task<ApiResponse<List<BannerDto>>> GetActiveAsync()
    {
        var banners = await _db.Banners
            .Where(b => b.IsActive)
            .OrderBy(b => b.SortOrder).ThenByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
        return ApiResponse<List<BannerDto>>.Ok(await MapListAsync(banners));
    }

    public async Task<ApiResponse<List<BannerDto>>> GetAllAsync()
    {
        var banners = await _db.Banners
            .OrderBy(b => b.SortOrder).ThenByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
        return ApiResponse<List<BannerDto>>.Ok(await MapListAsync(banners));
    }

    public async Task<ApiResponse<BannerDto>> CreateAsync(CreateBannerRequest request)
    {
        var banner = new Banner
        {
            Title = request.Title ?? string.Empty,
            CourseId = ParseId(request.CourseId),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        _db.Banners.Add(banner);
        await _db.SaveChangesAsync();
        return ApiResponse<BannerDto>.Ok(await MapAsync(banner), "Banner created.");
    }

    public async Task<ApiResponse<BannerDto>> UpdateAsync(Guid id, UpdateBannerRequest request)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id);
        if (banner is null) return ApiResponse<BannerDto>.Fail("Banner not found.");

        if (request.Title is not null) banner.Title = request.Title;
        if (request.CourseId is not null) banner.CourseId = ParseId(request.CourseId);
        if (request.IsActive.HasValue) banner.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue) banner.SortOrder = request.SortOrder.Value;

        banner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<BannerDto>.Ok(await MapAsync(banner), "Banner updated.");
    }

    public async Task<ApiResponse<BannerDto>> SetImageAsync(Guid id, IFormFile file)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id);
        if (banner is null) return ApiResponse<BannerDto>.Fail("Banner not found.");
        if (file is null || file.Length == 0)
            return ApiResponse<BannerDto>.Fail("No image uploaded.");

        banner.Image = await SaveFileAsync(file, "banners");
        banner.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<BannerDto>.Ok(await MapAsync(banner), "Banner image updated.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id);
        if (banner is null) return ApiResponse<bool>.Fail("Banner not found.");
        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Banner deleted.");
    }

    // -------------------------------------------------------------- helpers

    private static Guid? ParseId(string? raw) =>
        Guid.TryParse(raw, out var g) ? g : null;

    private async Task<List<BannerDto>> MapListAsync(List<Banner> banners)
    {
        var courseIds = banners.Where(b => b.CourseId.HasValue)
            .Select(b => b.CourseId!.Value).Distinct().ToList();
        var titles = await _db.Courses
            .Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Title);

        return banners.Select(b => Map(b,
            b.CourseId.HasValue && titles.TryGetValue(b.CourseId.Value, out var t) ? t : null))
            .ToList();
    }

    private async Task<BannerDto> MapAsync(Banner b)
    {
        string? title = null;
        if (b.CourseId.HasValue)
            title = await _db.Courses.Where(c => c.Id == b.CourseId.Value)
                .Select(c => c.Title).FirstOrDefaultAsync();
        return Map(b, title);
    }

    private BannerDto Map(Banner b, string? courseTitle) => new(
        b.Id.ToString(),
        b.Title,
        ResolveUrl(b.Image),
        b.CourseId?.ToString(),
        courseTitle,
        b.IsActive,
        b.SortOrder,
        b.CreatedAt.ToString("O"));

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8017").TrimEnd('/');
        return $"{baseUrl}/{path.TrimStart('/')}";
    }

    private async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var targetDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8017").TrimEnd('/');
        return $"{baseUrl}/uploads/{folder}/{fileName}";
    }
}
