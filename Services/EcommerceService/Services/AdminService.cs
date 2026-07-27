using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface IAdminService
{
    // Products
    Task<ApiResponse<List<AdminProductDto>>> GetProductsAsync(string? search, string? category, bool? isActive);
    Task<ApiResponse<AdminProductDto>> GetProductAsync(Guid id);
    Task<ApiResponse<AdminProductDto>> CreateProductAsync(AdminCreateProductRequest request);
    Task<ApiResponse<AdminProductDto>> UpdateProductAsync(Guid id, AdminUpdateProductRequest request);
    Task<ApiResponse<bool>> DeleteProductAsync(Guid id);
    Task<ApiResponse<AdminProductDto>> SetProductActiveAsync(Guid id, bool isActive);
    Task<ApiResponse<AdminProductDto>> SetStockAsync(Guid id, int stock);
    Task<ApiResponse<AdminProductDto>> SetMainImageAsync(Guid id, IFormFile file);
    Task<ApiResponse<AdminProductDto>> AddGalleryImageAsync(Guid id, IFormFile file);
    Task<ApiResponse<bool>> DeleteGalleryImageAsync(Guid id, Guid imageId);

    // Categories
    Task<ApiResponse<List<AdminCategoryDto>>> GetCategoriesAsync();
    Task<ApiResponse<AdminCategoryDto>> CreateCategoryAsync(AdminCreateCategoryRequest request);
    Task<ApiResponse<AdminCategoryDto>> UpdateCategoryAsync(Guid id, AdminUpdateCategoryRequest request);
    Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id);

    // Orders
    Task<ApiResponse<List<AdminOrderDto>>> GetOrdersAsync(string? status, string? userId);
    Task<ApiResponse<AdminOrderDto>> GetOrderAsync(Guid id);
    Task<ApiResponse<AdminOrderDto>> UpdateOrderStatusAsync(Guid id, string status);
    Task<ApiResponse<bool>> DeleteOrderAsync(Guid id);

    // Payment QRs
    Task<ApiResponse<List<AdminPaymentQrDto>>> GetPaymentQrsAsync();
    Task<ApiResponse<AdminPaymentQrDto>> CreatePaymentQrAsync(AdminCreatePaymentQrRequest request);
    Task<ApiResponse<AdminPaymentQrDto>> UpdatePaymentQrAsync(Guid id, AdminUpdatePaymentQrRequest request);
    Task<ApiResponse<bool>> DeletePaymentQrAsync(Guid id);

    // Notifications
    Task<ApiResponse<int>> SendNotificationAsync(AdminSendNotificationRequest request);
    Task<ApiResponse<List<AdminNotificationDto>>> GetNotificationsAsync(string? userId);

    // Dashboard
    Task<ApiResponse<AdminDashboardDto>> GetDashboardAsync();
}

public class AdminService : IAdminService
{
    private const int LowStockThreshold = 5;

    private readonly EcommerceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public AdminService(EcommerceDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    // ---------- Products ----------

    public async Task<ApiResponse<List<AdminProductDto>>> GetProductsAsync(string? search, string? category, bool? isActive)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) ||
                                     (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return ApiResponse<List<AdminProductDto>>.Ok(products.Select(MapProduct).ToList());
    }

    public async Task<ApiResponse<AdminProductDto>> GetProductAsync(Guid id)
    {
        var product = await LoadProductAsync(id);
        return product is null
            ? ApiResponse<AdminProductDto>.Fail("Product not found.")
            : ApiResponse<AdminProductDto>.Ok(MapProduct(product));
    }

    public async Task<ApiResponse<AdminProductDto>> CreateProductAsync(AdminCreateProductRequest request)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(request.CategoryId))
        {
            if (!Guid.TryParse(request.CategoryId, out var cid))
                return ApiResponse<AdminProductDto>.Fail("Invalid category id.");
            if (!await _db.ProductCategories.AnyAsync(c => c.Id == cid))
                return ApiResponse<AdminProductDto>.Fail("Category not found.");
            categoryId = cid;
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            IsActive = request.IsActive,
            CategoryId = categoryId,
            Image = request.Image
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return ApiResponse<AdminProductDto>.Ok(MapProduct((await LoadProductAsync(product.Id))!), "Product created.");
    }

    public async Task<ApiResponse<AdminProductDto>> UpdateProductAsync(Guid id, AdminUpdateProductRequest request)
    {
        var product = await LoadProductAsync(id);
        if (product is null)
            return ApiResponse<AdminProductDto>.Fail("Product not found.");

        if (request.Name is not null) product.Name = request.Name;
        if (request.Description is not null) product.Description = request.Description;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.Stock.HasValue) product.Stock = request.Stock.Value;
        if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;
        if (request.Image is not null) product.Image = request.Image;

        if (request.CategoryId is not null)
        {
            if (request.CategoryId.Length == 0)
            {
                product.CategoryId = null;
            }
            else
            {
                if (!Guid.TryParse(request.CategoryId, out var cid))
                    return ApiResponse<AdminProductDto>.Fail("Invalid category id.");
                if (!await _db.ProductCategories.AnyAsync(c => c.Id == cid))
                    return ApiResponse<AdminProductDto>.Fail("Category not found.");
                product.CategoryId = cid;
            }
        }

        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<AdminProductDto>.Ok(MapProduct((await LoadProductAsync(id))!), "Product updated.");
    }

    public async Task<ApiResponse<bool>> DeleteProductAsync(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
            return ApiResponse<bool>.Fail("Product not found.");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Product deleted.");
    }

    public async Task<ApiResponse<AdminProductDto>> SetProductActiveAsync(Guid id, bool isActive)
    {
        var product = await LoadProductAsync(id);
        if (product is null)
            return ApiResponse<AdminProductDto>.Fail("Product not found.");

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminProductDto>.Ok(MapProduct(product));
    }

    public async Task<ApiResponse<AdminProductDto>> SetStockAsync(Guid id, int stock)
    {
        var product = await LoadProductAsync(id);
        if (product is null)
            return ApiResponse<AdminProductDto>.Fail("Product not found.");

        product.Stock = stock;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminProductDto>.Ok(MapProduct(product));
    }

    public async Task<ApiResponse<AdminProductDto>> SetMainImageAsync(Guid id, IFormFile file)
    {
        var product = await LoadProductAsync(id);
        if (product is null)
            return ApiResponse<AdminProductDto>.Fail("Product not found.");

        product.Image = await SaveFileAsync(file, "products");
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminProductDto>.Ok(MapProduct(product), "Image updated.");
    }

    public async Task<ApiResponse<AdminProductDto>> AddGalleryImageAsync(Guid id, IFormFile file)
    {
        var product = await LoadProductAsync(id);
        if (product is null)
            return ApiResponse<AdminProductDto>.Fail("Product not found.");

        var path = await SaveFileAsync(file, "products");
        _db.ProductImages.Add(new ProductImage { ProductId = id, Image = path });
        await _db.SaveChangesAsync();
        return ApiResponse<AdminProductDto>.Ok(MapProduct((await LoadProductAsync(id))!), "Image added.");
    }

    public async Task<ApiResponse<bool>> DeleteGalleryImageAsync(Guid id, Guid imageId)
    {
        var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id);
        if (image is null)
            return ApiResponse<bool>.Fail("Image not found.");

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Image deleted.");
    }

    // ---------- Categories ----------

    public async Task<ApiResponse<List<AdminCategoryDto>>> GetCategoriesAsync()
    {
        var cats = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();

        var counts = await _db.Products
            .Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = counts.ToDictionary(x => x.CategoryId, x => x.Count);

        var result = cats
            .Select(c => MapCategory(c, countMap.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();

        return ApiResponse<List<AdminCategoryDto>>.Ok(result);
    }

    public async Task<ApiResponse<AdminCategoryDto>> CreateCategoryAsync(AdminCreateCategoryRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(request.Name) : Slugify(request.Slug);

        if (await _db.ProductCategories.AnyAsync(c => c.Slug == slug))
            return ApiResponse<AdminCategoryDto>.Fail("A category with this slug already exists.");

        var category = new ProductCategory
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description
        };

        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync();
        return ApiResponse<AdminCategoryDto>.Ok(MapCategory(category, 0), "Category created.");
    }

    public async Task<ApiResponse<AdminCategoryDto>> UpdateCategoryAsync(Guid id, AdminUpdateCategoryRequest request)
    {
        var category = await _db.ProductCategories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
            return ApiResponse<AdminCategoryDto>.Fail("Category not found.");

        if (request.Name is not null) category.Name = request.Name;
        if (request.Description is not null) category.Description = request.Description;

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            var slug = Slugify(request.Slug);
            if (await _db.ProductCategories.AnyAsync(c => c.Slug == slug && c.Id != id))
                return ApiResponse<AdminCategoryDto>.Fail("A category with this slug already exists.");
            category.Slug = slug;
        }

        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminCategoryDto>.Ok(MapCategory(category, category.Products.Count), "Category updated.");
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
    {
        var category = await _db.ProductCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
            return ApiResponse<bool>.Fail("Category not found.");

        _db.ProductCategories.Remove(category);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Category deleted.");
    }

    // ---------- Orders ----------

    public async Task<ApiResponse<List<AdminOrderDto>>> GetOrdersAsync(string? status, string? userId)
    {
        var query = _db.Orders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var uid))
            query = query.Where(o => o.UserId == uid);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return ApiResponse<List<AdminOrderDto>>.Ok(orders.Select(MapOrder).ToList());
    }

    public async Task<ApiResponse<AdminOrderDto>> GetOrderAsync(Guid id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        return order is null
            ? ApiResponse<AdminOrderDto>.Fail("Order not found.")
            : ApiResponse<AdminOrderDto>.Ok(MapOrder(order));
    }

    public async Task<ApiResponse<AdminOrderDto>> UpdateOrderStatusAsync(Guid id, string status)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
            return ApiResponse<AdminOrderDto>.Fail("Order not found.");

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminOrderDto>.Ok(MapOrder(order), "Order status updated.");
    }

    public async Task<ApiResponse<bool>> DeleteOrderAsync(Guid id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
            return ApiResponse<bool>.Fail("Order not found.");

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Order deleted.");
    }

    // ---------- Payment QRs ----------

    public async Task<ApiResponse<List<AdminPaymentQrDto>>> GetPaymentQrsAsync()
    {
        var qrs = await _db.PaymentQrs.OrderByDescending(q => q.CreatedAt).ToListAsync();
        return ApiResponse<List<AdminPaymentQrDto>>.Ok(qrs.Select(MapPaymentQr).ToList());
    }

    public async Task<ApiResponse<AdminPaymentQrDto>> CreatePaymentQrAsync(AdminCreatePaymentQrRequest request)
    {
        var qr = new PaymentQr
        {
            VendorId = request.VendorId,
            VendorName = request.VendorName,
            Name = request.Name,
            Image = request.Image ?? string.Empty,
            IsActive = request.IsActive
        };

        _db.PaymentQrs.Add(qr);
        await _db.SaveChangesAsync();
        return ApiResponse<AdminPaymentQrDto>.Ok(MapPaymentQr(qr), "Payment QR created.");
    }

    public async Task<ApiResponse<AdminPaymentQrDto>> UpdatePaymentQrAsync(Guid id, AdminUpdatePaymentQrRequest request)
    {
        var qr = await _db.PaymentQrs.FirstOrDefaultAsync(q => q.Id == id);
        if (qr is null)
            return ApiResponse<AdminPaymentQrDto>.Fail("Payment QR not found.");

        if (request.VendorId is not null) qr.VendorId = request.VendorId;
        if (request.VendorName is not null) qr.VendorName = request.VendorName;
        if (request.Name is not null) qr.Name = request.Name;
        if (request.Image is not null) qr.Image = request.Image;
        if (request.IsActive.HasValue) qr.IsActive = request.IsActive.Value;

        qr.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<AdminPaymentQrDto>.Ok(MapPaymentQr(qr), "Payment QR updated.");
    }

    public async Task<ApiResponse<bool>> DeletePaymentQrAsync(Guid id)
    {
        var qr = await _db.PaymentQrs.FirstOrDefaultAsync(q => q.Id == id);
        if (qr is null)
            return ApiResponse<bool>.Fail("Payment QR not found.");

        _db.PaymentQrs.Remove(qr);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Payment QR deleted.");
    }

    // ---------- Notifications ----------

    public async Task<ApiResponse<int>> SendNotificationAsync(AdminSendNotificationRequest request)
    {
        List<Guid> recipients;

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            if (!Guid.TryParse(request.UserId, out var uid))
                return ApiResponse<int>.Fail("Invalid user id.");
            recipients = new List<Guid> { uid };
        }
        else
        {
            // Broadcast: every user who has ever registered a device token.
            recipients = await _db.FcmTokens.Select(t => t.UserId).Distinct().ToListAsync();
        }

        foreach (var userId in recipients)
        {
            _db.Notifications.Add(new EcommerceNotification
            {
                UserId = userId,
                Title = request.Title,
                Message = request.Message,
                NotificationType = request.NotificationType,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync();
        return ApiResponse<int>.Ok(recipients.Count, $"Notification sent to {recipients.Count} user(s).");
    }

    public async Task<ApiResponse<List<AdminNotificationDto>>> GetNotificationsAsync(string? userId)
    {
        var query = _db.Notifications.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var uid))
            query = query.Where(n => n.UserId == uid);

        var notifications = await query.OrderByDescending(n => n.CreatedAt).Take(200).ToListAsync();

        return ApiResponse<List<AdminNotificationDto>>.Ok(notifications.Select(n => new AdminNotificationDto(
            n.Id.ToString(),
            n.UserId.ToString(),
            n.Title,
            n.Message,
            n.NotificationType,
            n.IsRead,
            n.CreatedAt.ToString("O"))).ToList());
    }

    // ---------- Dashboard ----------

    public async Task<ApiResponse<AdminDashboardDto>> GetDashboardAsync()
    {
        var totalProducts = await _db.Products.CountAsync();
        var activeProducts = await _db.Products.CountAsync(p => p.IsActive);
        var lowStock = await _db.Products.CountAsync(p => p.Stock <= LowStockThreshold);
        var totalCategories = await _db.ProductCategories.CountAsync();
        var totalOrders = await _db.Orders.CountAsync();
        var pendingOrders = await _db.Orders.CountAsync(o => o.Status == "pending" || o.Status == "payment_submitted");

        // Revenue counts orders that have moved past the unpaid stage.
        var paidStatuses = new[] { "paid", "processing", "shipped", "delivered", "completed" };
        var totalRevenue = await _db.Orders
            .Where(o => paidStatuses.Contains(o.Status))
            .SumAsync(o => (decimal?)o.GrandTotal) ?? 0m;

        var recent = await _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync();

        var dashboard = new AdminDashboardDto(
            totalProducts,
            activeProducts,
            lowStock,
            totalCategories,
            totalOrders,
            pendingOrders,
            totalRevenue,
            recent.Select(MapOrder).ToList());

        return ApiResponse<AdminDashboardDto>.Ok(dashboard);
    }

    // ---------- Helpers ----------

    private Task<Product?> LoadProductAsync(Guid id) =>
        _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

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

    private AdminProductDto MapProduct(Product p) => new(
        p.Id.ToString(),
        p.Name,
        p.Description,
        p.Price,
        p.Stock,
        p.IsActive,
        p.CategoryId?.ToString(),
        p.Category?.Name,
        ResolveUrl(p.Image),
        p.Images.Select(i => new AdminProductImageDto(i.Id.ToString(), ResolveUrl(i.Image) ?? i.Image)).ToList(),
        p.CreatedAt.ToString("O"),
        p.UpdatedAt.ToString("O"));

    private static AdminCategoryDto MapCategory(ProductCategory c, int productCount) => new(
        c.Id.ToString(),
        c.Name,
        c.Slug,
        c.Description,
        productCount,
        c.CreatedAt.ToString("O"));

    private AdminOrderDto MapOrder(Order o) => new(
        o.Id.ToString(),
        o.UserId.ToString(),
        o.FullName,
        o.Address,
        o.PhoneNumber,
        o.Notes,
        o.PaymentType,
        o.Status,
        o.TotalAmount,
        o.ShippingCharge,
        o.GrandTotal,
        ResolveUrl(o.PaymentScreenshotPath),
        o.KhaltiPidx,
        o.CreatedAt.ToString("O"),
        o.Items.Select(i => new AdminOrderItemDto(
            i.ProductId.ToString(),
            i.ProductName,
            ResolveUrl(i.Image),
            i.Price,
            i.Quantity,
            i.LineTotal)).ToList());

    private AdminPaymentQrDto MapPaymentQr(PaymentQr q) => new(
        q.Id.ToString(),
        q.VendorId,
        q.VendorName,
        q.Name,
        ResolveUrl(q.Image),
        q.IsActive,
        q.CreatedAt.ToString("O"));

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = _config["PublicBaseUrl"] ?? "http://localhost:8016";
        return $"{baseUrl}{path}";
    }

    private static string Slugify(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-');
    }
}
