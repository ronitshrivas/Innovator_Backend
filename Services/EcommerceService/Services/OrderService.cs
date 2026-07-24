using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface IOrderService
{
    Task<ApiResponse<CheckoutSummaryResponse>> CheckoutAsync(Guid userId, CheckoutRequest request);
    Task<ApiResponse<bool>> ConfirmPaymentAsync(Guid userId, Guid orderId, IFormFile screenshot);
    Task<ApiResponse<KhaltiPaymentResponse>> InitiateKhaltiAsync(Guid userId, string orderId);
    Task<ApiResponse<List<PaymentQrDto>>> GetPaymentQrsAsync();
}

public class OrderService : IOrderService
{
    private readonly EcommerceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public OrderService(EcommerceDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    public async Task<ApiResponse<CheckoutSummaryResponse>> CheckoutAsync(
        Guid userId, CheckoutRequest request)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
            return ApiResponse<CheckoutSummaryResponse>.Fail("Your cart is empty.");

        var shippingCharge = 0m;
        var totalAmount = cart.Items.Sum(ci => ci.Product.Price * ci.Quantity);
        var grandTotal = totalAmount + shippingCharge;

        var order = new Order
        {
            UserId = userId,
            FullName = request.FullName,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            Notes = request.Notes,
            PaymentType = request.PaymentType,
            Status = "pending",
            TotalAmount = totalAmount,
            ShippingCharge = shippingCharge,
            GrandTotal = grandTotal
        };

        foreach (var ci in cart.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product.Name,
                Price = ci.Product.Price,
                Quantity = ci.Quantity,
                LineTotal = ci.Product.Price * ci.Quantity,
                Image = ci.Product.Image
            });

            ci.Product.Stock = Math.Max(0, ci.Product.Stock - ci.Quantity);
        }

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync();

        var requiresKhalti = request.PaymentType.ToLower() == "khalti";

        return ApiResponse<CheckoutSummaryResponse>.Ok(new CheckoutSummaryResponse(
            Message: "Order placed successfully.",
            OrderId: order.Id.ToString(),
            FullName: order.FullName,
            Address: order.Address,
            PhoneNumber: order.PhoneNumber,
            PaymentType: order.PaymentType,
            Items: order.Items.Select(i => new CheckoutOrderItemDto(
                i.ProductId.ToString(),
                i.ProductName,
                ResolveUrl(i.Image),
                (double)i.Price,
                i.Quantity,
                (double)i.LineTotal)).ToList(),
            TotalItems: order.Items.Sum(i => i.Quantity),
            TotalAmount: (double)totalAmount,
            ShippingCharge: (double)shippingCharge,
            GrandTotal: (double)grandTotal,
            Status: order.Status,
            RequiresKhaltiPayment: requiresKhalti));
    }

    public async Task<ApiResponse<bool>> ConfirmPaymentAsync(
        Guid userId, Guid orderId, IFormFile screenshot)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
            return ApiResponse<bool>.Fail("Order not found.");

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "payment-screenshots");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(screenshot.FileName);
        var fileName = $"{orderId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using var stream = File.Create(fullPath);
        await screenshot.CopyToAsync(stream);

        order.PaymentScreenshotPath = $"/payment-screenshots/{fileName}";
        order.Status = "payment_submitted";
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<KhaltiPaymentResponse>> InitiateKhaltiAsync(
        Guid userId, string orderId)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            return ApiResponse<KhaltiPaymentResponse>.Fail("Invalid order id.");

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderGuid && o.UserId == userId);

        if (order == null)
            return ApiResponse<KhaltiPaymentResponse>.Fail("Order not found.");

        var pidx = Guid.NewGuid().ToString("N");
        var khaltiBaseUrl = _config["Khalti:BaseUrl"] ?? "https://khalti.com/pay";
        var paymentUrl = $"{khaltiBaseUrl}?pidx={pidx}";

        order.KhaltiPidx = pidx;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<KhaltiPaymentResponse>.Ok(new KhaltiPaymentResponse(
            Pidx: pidx,
            PaymentUrl: paymentUrl,
            OrderId: orderId,
            Amount: (double)order.GrandTotal));
    }

    public async Task<ApiResponse<List<PaymentQrDto>>> GetPaymentQrsAsync()
    {
        var qrs = await _db.PaymentQrs
            .Where(q => q.IsActive)
            .ToListAsync();

        return ApiResponse<List<PaymentQrDto>>.Ok(
            qrs.Select(q => new PaymentQrDto(
                q.Id.ToString(),
                q.VendorId,
                q.VendorName,
                q.Name,
                ResolveUrl(q.Image) ?? q.Image)).ToList());
    }

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = _config["PublicBaseUrl"] ?? "http://localhost:8016";
        return $"{baseUrl}{path}";
    }
}
