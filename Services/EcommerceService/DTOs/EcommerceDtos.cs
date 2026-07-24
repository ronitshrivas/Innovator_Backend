using System.ComponentModel.DataAnnotations;

namespace EcommerceService.DTOs;

public record CategoryDetailsDto(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string CreatedAt
);

public record ProductDto(
    string Id,
    string Name,
    string? Description,
    string Price,
    int Stock,
    bool IsActive,
    string? Category,
    CategoryDetailsDto? CategoryDetails,
    string? Image
);

public record ProductDetailDto(
    string Id,
    string Name,
    string? Description,
    string Price,
    int Stock,
    bool IsActive,
    string? Category,
    CategoryDetailsDto? CategoryDetails,
    string? Image,
    List<ProductImageDto> Images,
    string? CreatedAt,
    string? UpdatedAt
);

public record ProductImageDto(
    int Id,
    string Image
);

public record CartItemDto(
    string Id,
    string Cart,
    string Product,
    string ProductName,
    double Price,
    int Quantity,
    double Total
);

public record AddCartItemRequest(
    [Required] string Product
);

public record UpdateCartItemRequest(
    [Required, Range(1, 100)] int Quantity
);

public record CheckoutRequest(
    [Required, MaxLength(150)] string FullName,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(20)] string PhoneNumber,
    string? Notes,
    [Required] string PaymentType
);

public record CheckoutOrderItemDto(
    string ProductId,
    string ProductName,
    string? Image,
    double Price,
    int Quantity,
    double LineTotal
);

public record CheckoutSummaryResponse(
    string Message,
    string OrderId,
    string FullName,
    string Address,
    string PhoneNumber,
    string PaymentType,
    List<CheckoutOrderItemDto> Items,
    int TotalItems,
    double TotalAmount,
    double ShippingCharge,
    double GrandTotal,
    string Status,
    bool? RequiresKhaltiPayment
);

public record PaymentQrDto(
    string Id,
    string VendorId,
    string VendorName,
    string Name,
    string Image
);

public record KhaltiPaymentResponse(
    string Pidx,
    string PaymentUrl,
    string OrderId,
    double Amount
);

public record InitiateKhaltiRequest(
    [Required] string OrderId
);

public record FcmTokenRequest(
    [Required] string Token,
    string Platform = "android"
);

public record NotificationDto(
    string Id,
    string Title,
    string Message,
    string NotificationType,
    bool IsRead,
    string CreatedAt,
    NotificationDataDto Data
);

public record NotificationDataDto(
    string Type,
    string ProductId,
    string Category
);
