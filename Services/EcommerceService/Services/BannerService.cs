using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface IBannerService
{
    Task<ApiResponse<List<BannerDto>>> GetActiveAsync();
    Task<ApiResponse<List<BannerDto>>> GetAllAsync();
    Task<ApiResponse<BannerDto>> CreateAsync(AdminCreateBannerRequest request);
    Task<ApiResponse<BannerDto>> UpdateAsync(Guid id, AdminUpdateBannerRequest request);
    Task<ApiResponse<BannerDto>> SetImageAsync(Guid id, IFormFile file);
    Task<ApiResponse<bool>> DeleteAsync(Guid id);
}

public class BannerService : IBannerService
{
    private readonly EcommerceDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public BannerService(EcommerceDbContext db, IConfiguration config, IWebHostEnvironment env)
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

    public async Task<ApiResponse<BannerDto>> CreateAsync(AdminCreateBannerRequest request)
    {
        var banner = new Banner
        {
            Title = request.Title ?? string.Empty,
            ProductId = ParseId(request.ProductId),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        _db.Banners.Add(banner);
        await _db.SaveChangesAsync();
        return ApiResponse<BannerDto>.Ok(await MapAsync(banner), "Banner created.");
    }

    public async Task<ApiResponse<BannerDto>> UpdateAsync(Guid id, AdminUpdateBannerRequest request)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id);
        if (banner is null) return ApiResponse<BannerDto>.Fail("Banner not found.");

        if (request.Title is not null) banner.Title = request.Title;
        if (request.ProductId is not null) banner.ProductId = ParseId(request.ProductId);
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
        var productIds = banners.Where(b => b.ProductId.HasValue)
            .Select(b => b.ProductId!.Value).Distinct().ToList();
        var names = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return banners.Select(b => Map(b,
            b.ProductId.HasValue && names.TryGetValue(b.ProductId.Value, out var n) ? n : null))
            .ToList();
    }

    private async Task<BannerDto> MapAsync(Banner b)
    {
        string? name = null;
        if (b.ProductId.HasValue)
            name = await _db.Products.Where(p => p.Id == b.ProductId.Value)
                .Select(p => p.Name).FirstOrDefaultAsync();
        return Map(b, name);
    }

    private BannerDto Map(Banner b, string? productName) => new(
        b.Id.ToString(),
        b.Title,
        ResolveUrl(b.Image),
        b.ProductId?.ToString(),
        productName,
        b.IsActive,
        b.SortOrder,
        b.CreatedAt.ToString("O"));

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8016").TrimEnd('/');
        return $"{baseUrl}/{path.TrimStart('/')}";
    }

    private async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var targetDir = Path.Combine(webRoot, folder);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/{folder}/{fileName}";
    }
}
