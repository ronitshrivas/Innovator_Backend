using EcommerceService.DTOs;
using EcommerceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceService.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    private IActionResult Respond<T>(Innovator.Shared.DTOs.ApiResponse<T> result) =>
        result.Success ? Ok(result) : BadRequest(result);

    // ---------- Dashboard ----------

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard() => Respond(await _admin.GetDashboardAsync());

    // ---------- Products ----------

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool? isActive) =>
        Respond(await _admin.GetProductsAsync(search, category, isActive));

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id) => Respond(await _admin.GetProductAsync(id));

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] AdminCreateProductRequest request) =>
        Respond(await _admin.CreateProductAsync(request));

    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] AdminUpdateProductRequest request) =>
        Respond(await _admin.UpdateProductAsync(id, request));

    [HttpDelete("products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id) => Respond(await _admin.DeleteProductAsync(id));

    [HttpPatch("products/{id:guid}/active")]
    public async Task<IActionResult> SetProductActive(Guid id, [FromQuery] bool isActive) =>
        Respond(await _admin.SetProductActiveAsync(id, isActive));

    [HttpPatch("products/{id:guid}/stock")]
    public async Task<IActionResult> SetStock(Guid id, [FromBody] AdminSetStockRequest request) =>
        Respond(await _admin.SetStockAsync(id, request.Stock));

    [HttpPost("products/{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SetMainImage(Guid id, IFormFile image) =>
        Respond(await _admin.SetMainImageAsync(id, image));

    [HttpPost("products/{id:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddGalleryImage(Guid id, IFormFile image) =>
        Respond(await _admin.AddGalleryImageAsync(id, image));

    [HttpDelete("products/{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteGalleryImage(Guid id, Guid imageId) =>
        Respond(await _admin.DeleteGalleryImageAsync(id, imageId));

    // ---------- Categories ----------

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Respond(await _admin.GetCategoriesAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] AdminCreateCategoryRequest request) =>
        Respond(await _admin.CreateCategoryAsync(request));

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] AdminUpdateCategoryRequest request) =>
        Respond(await _admin.UpdateCategoryAsync(id, request));

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id) => Respond(await _admin.DeleteCategoryAsync(id));

    // ---------- Orders ----------

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] string? userId) =>
        Respond(await _admin.GetOrdersAsync(status, userId));

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id) => Respond(await _admin.GetOrderAsync(id));

    [HttpPatch("orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] AdminUpdateOrderStatusRequest request) =>
        Respond(await _admin.UpdateOrderStatusAsync(id, request.Status));

    [HttpDelete("orders/{id:guid}")]
    public async Task<IActionResult> DeleteOrder(Guid id) => Respond(await _admin.DeleteOrderAsync(id));

    // ---------- Payment QRs ----------

    [HttpGet("payment-qrs")]
    public async Task<IActionResult> GetPaymentQrs() => Respond(await _admin.GetPaymentQrsAsync());

    [HttpPost("payment-qrs")]
    public async Task<IActionResult> CreatePaymentQr([FromBody] AdminCreatePaymentQrRequest request) =>
        Respond(await _admin.CreatePaymentQrAsync(request));

    [HttpPut("payment-qrs/{id:guid}")]
    public async Task<IActionResult> UpdatePaymentQr(Guid id, [FromBody] AdminUpdatePaymentQrRequest request) =>
        Respond(await _admin.UpdatePaymentQrAsync(id, request));

    [HttpDelete("payment-qrs/{id:guid}")]
    public async Task<IActionResult> DeletePaymentQr(Guid id) => Respond(await _admin.DeletePaymentQrAsync(id));

    // ---------- Notifications ----------

    [HttpPost("notifications")]
    public async Task<IActionResult> SendNotification([FromBody] AdminSendNotificationRequest request) =>
        Respond(await _admin.SendNotificationAsync(request));

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] string? userId) =>
        Respond(await _admin.GetNotificationsAsync(userId));
}

// Admin management of shop banners (image + linked product).
[ApiController]
[Route("api/admin/banners")]
[Authorize(Roles = "admin")]
public class AdminBannerController : ControllerBase
{
    private readonly IBannerService _banners;

    public AdminBannerController(IBannerService banners) => _banners = banners;

    private IActionResult Respond<T>(Innovator.Shared.DTOs.ApiResponse<T> result) =>
        result.Success ? Ok(result) : BadRequest(result);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Respond(await _banners.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateBannerRequest request) =>
        Respond(await _banners.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUpdateBannerRequest request) =>
        Respond(await _banners.UpdateAsync(id, request));

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SetImage(Guid id, IFormFile image) =>
        Respond(await _banners.SetImageAsync(id, image));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        Respond(await _banners.DeleteAsync(id));
}
