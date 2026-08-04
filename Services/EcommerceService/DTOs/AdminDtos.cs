using System.ComponentModel.DataAnnotations;

namespace EcommerceService.DTOs;

// ---------- Products ----------

public record AdminProductDto(
    string Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    bool IsActive,
    string? CategoryId,
    string? CategoryName,
    string? Image,
    List<AdminProductImageDto> Images,
    string CreatedAt,
    string UpdatedAt
);

public record AdminProductImageDto(
    string Id,
    string Image
);

public record AdminCreateProductRequest(
    [Required, MaxLength(255)] string Name,
    string? Description,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int Stock,
    bool IsActive = true,
    string? CategoryId = null,
    string? Image = null
);

public record AdminUpdateProductRequest(
    string? Name,
    string? Description,
    decimal? Price,
    int? Stock,
    bool? IsActive,
    string? CategoryId,
    string? Image
);

public record AdminSetStockRequest(
    [Range(0, int.MaxValue)] int Stock
);

// ---------- Categories ----------

public record AdminCategoryDto(
    string Id,
    string Name,
    string Slug,
    string? Description,
    int ProductCount,
    string CreatedAt
);

public record AdminCreateCategoryRequest(
    [Required, MaxLength(100)] string Name,
    string? Slug,
    string? Description
);

public record AdminUpdateCategoryRequest(
    string? Name,
    string? Slug,
    string? Description
);

// ---------- Orders ----------

public record AdminOrderItemDto(
    string ProductId,
    string ProductName,
    string? Image,
    decimal Price,
    int Quantity,
    decimal LineTotal
);

public record AdminOrderDto(
    string Id,
    string UserId,
    string FullName,
    string Address,
    string PhoneNumber,
    string? Notes,
    string PaymentType,
    string Status,
    decimal TotalAmount,
    decimal ShippingCharge,
    decimal GrandTotal,
    string? PaymentScreenshot,
    string? KhaltiPidx,
    string CreatedAt,
    List<AdminOrderItemDto> Items
);

public record AdminUpdateOrderStatusRequest(
    [Required] string Status
);

// ---------- Payment QRs ----------

public record AdminPaymentQrDto(
    string Id,
    string VendorId,
    string VendorName,
    string Name,
    string? Image,
    bool IsActive,
    string CreatedAt
);

public record AdminCreatePaymentQrRequest(
    [Required] string VendorId,
    [Required] string VendorName,
    [Required] string Name,
    string? Image,
    bool IsActive = true
);

public record AdminUpdatePaymentQrRequest(
    string? VendorId,
    string? VendorName,
    string? Name,
    string? Image,
    bool? IsActive
);

// ---------- Notifications ----------

public record AdminSendNotificationRequest(
    string? UserId,
    [Required] string Title,
    [Required] string Message,
    string NotificationType = "admin"
);

public record AdminNotificationDto(
    string Id,
    string UserId,
    string Title,
    string Message,
    string NotificationType,
    bool IsRead,
    string CreatedAt
);

// ---------- Banners ----------

public record BannerDto(
    string Id,
    string Title,
    string? Image,
    string? ProductId,
    string? ProductName,
    bool IsActive,
    int SortOrder,
    string CreatedAt
);

public record AdminCreateBannerRequest(
    string? Title,
    string? ProductId,
    bool IsActive = true,
    int SortOrder = 0
);

public record AdminUpdateBannerRequest(
    string? Title,
    string? ProductId,
    bool? IsActive,
    int? SortOrder
);

// ---------- Dashboard ----------

public record AdminDashboardDto(
    int TotalProducts,
    int ActiveProducts,
    int LowStockProducts,
    int TotalCategories,
    int TotalOrders,
    int PendingOrders,
    decimal TotalRevenue,
    List<AdminOrderDto> RecentOrders
);
