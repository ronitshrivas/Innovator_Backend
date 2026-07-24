using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface IProductService
{
    Task<ApiResponse<List<ProductDto>>> GetProductsAsync(string? category, string? search);
    Task<ApiResponse<ProductDetailDto>> GetProductByIdAsync(Guid productId);
    Task<ApiResponse<List<CategoryDetailsDto>>> GetCategoriesAsync();
}

public class ProductService : IProductService
{
    private readonly EcommerceDbContext _db;
    private readonly IConfiguration _config;

    public ProductService(EcommerceDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ApiResponse<List<ProductDto>>> GetProductsAsync(
        string? category, string? search)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category != null &&
                                     p.Category.Slug == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) ||
                                     (p.Description != null &&
                                      p.Description.ToLower().Contains(search.ToLower())));

        var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        return ApiResponse<List<ProductDto>>.Ok(
            products.Select(p => MapToDto(p)).ToList());
    }

    public async Task<ApiResponse<ProductDetailDto>> GetProductByIdAsync(Guid productId)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return ApiResponse<ProductDetailDto>.Fail("Product not found.");

        return ApiResponse<ProductDetailDto>.Ok(MapToDetailDto(product));
    }

    public async Task<ApiResponse<List<CategoryDetailsDto>>> GetCategoriesAsync()
    {
        var cats = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
        return ApiResponse<List<CategoryDetailsDto>>.Ok(
            cats.Select(c => new CategoryDetailsDto(
                c.Id.ToString(),
                c.Name,
                c.Slug,
                c.Description,
                c.CreatedAt.ToString("O"))).ToList());
    }

    private ProductDto MapToDto(Product p) =>
        new(p.Id.ToString(),
            p.Name,
            p.Description,
            p.Price.ToString("F2"),
            p.Stock,
            p.IsActive,
            p.CategoryId?.ToString(),
            p.Category != null
                ? new CategoryDetailsDto(
                    p.Category.Id.ToString(),
                    p.Category.Name,
                    p.Category.Slug,
                    p.Category.Description,
                    p.Category.CreatedAt.ToString("O"))
                : null,
            ResolveUrl(p.Image));

    private ProductDetailDto MapToDetailDto(Product p) =>
        new(p.Id.ToString(),
            p.Name,
            p.Description,
            p.Price.ToString("F2"),
            p.Stock,
            p.IsActive,
            p.CategoryId?.ToString(),
            p.Category != null
                ? new CategoryDetailsDto(
                    p.Category.Id.ToString(),
                    p.Category.Name,
                    p.Category.Slug,
                    p.Category.Description,
                    p.Category.CreatedAt.ToString("O"))
                : null,
            ResolveUrl(p.Image),
            p.Images.Select((img, i) => new ProductImageDto(i + 1, ResolveUrl(img.Image) ?? "")).ToList(),
            p.CreatedAt.ToString("O"),
            p.UpdatedAt.ToString("O"));

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = _config["PublicBaseUrl"] ?? "http://localhost:8016";
        return $"{baseUrl}{path}";
    }
}
