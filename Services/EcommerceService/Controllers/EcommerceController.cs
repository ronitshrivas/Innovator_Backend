using EcommerceService.DTOs;
using EcommerceService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceService.Controllers;

[ApiController]
[Route("api")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService) =>
        _productService = productService;

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var result = await _productService.GetProductsAsync(category, search);
        return Ok(result.Data);
    }

    [HttpGet("products/{productId}")]
    public async Task<IActionResult> GetProductDetail(string productId)
    {
        if (!Guid.TryParse(productId, out var id))
            return BadRequest(new { message = "Invalid product id." });

        var result = await _productService.GetProductByIdAsync(id);
        return result.Success ? Ok(result.Data) : NotFound(new { message = result.Message });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _productService.GetCategoriesAsync();
        return Ok(result.Data);
    }
}

// Public banners for the shop home carousel.
[ApiController]
[Route("api")]
public class BannerController : ControllerBase
{
    private readonly IBannerService _banners;

    public BannerController(IBannerService banners) => _banners = banners;

    [HttpGet("banners")]
    public async Task<IActionResult> GetBanners()
    {
        var result = await _banners.GetActiveAsync();
        return Ok(result.Data);
    }
}

[ApiController]
[Route("api/cart-items")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService) => _cartService = cartService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var result = await _cartService.GetCartAsync(CurrentUserId);
        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
    {
        var result = await _cartService.AddItemAsync(CurrentUserId, request.Product);
        return result.Success ? StatusCode(201, result.Data) : BadRequest(new { message = result.Message });
    }

    [HttpPatch("{cartItemId}")]
    public async Task<IActionResult> UpdateItem(
        string cartItemId, [FromBody] UpdateCartItemRequest request)
    {
        if (!Guid.TryParse(cartItemId, out var id))
            return BadRequest(new { message = "Invalid cart item id." });

        var result = await _cartService.UpdateItemAsync(CurrentUserId, id, request.Quantity);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Message });
    }

    [HttpDelete("{cartItemId}")]
    public async Task<IActionResult> DeleteItem(string cartItemId)
    {
        if (!Guid.TryParse(cartItemId, out var id))
            return BadRequest(new { message = "Invalid cart item id." });

        var result = await _cartService.DeleteItemAsync(CurrentUserId, id);
        return result.Success ? NoContent() : BadRequest(new { message = result.Message });
    }
}

[ApiController]
[Route("api")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService) => _orderService = orderService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpPost("checkout/summary")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    {
        var result = await _orderService.CheckoutAsync(CurrentUserId, request);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Message });
    }

    [HttpPost("orders/{orderId}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(
        string orderId, IFormFile paymentScreenshot)
    {
        if (!Guid.TryParse(orderId, out var id))
            return BadRequest(new { message = "Invalid order id." });

        var result = await _orderService.ConfirmPaymentAsync(CurrentUserId, id, paymentScreenshot);
        return result.Success ? Ok(new { message = "Payment confirmed." }) : BadRequest(new { message = result.Message });
    }

    [HttpPost("payments/initiate")]
    public async Task<IActionResult> InitiateKhalti([FromBody] InitiateKhaltiRequest request)
    {
        var result = await _orderService.InitiateKhaltiAsync(CurrentUserId, request.OrderId);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Message });
    }

    [HttpGet("payment-qrs/public-list")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaymentQrs()
    {
        var result = await _orderService.GetPaymentQrsAsync();
        return Ok(result.Data);
    }
}

[ApiController]
[Route("api")]
[Authorize]
public class EcommerceNotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public EcommerceNotificationController(INotificationService notificationService) =>
        _notificationService = notificationService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpPost("fcm-tokens")]
    public async Task<IActionResult> RegisterFcmToken([FromBody] FcmTokenRequest request)
    {
        var result = await _notificationService.RegisterFcmTokenAsync(CurrentUserId, request);
        return result.Success ? StatusCode(201, new { message = "Token registered." }) : BadRequest();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var result = await _notificationService.GetNotificationsAsync(CurrentUserId);
        return Ok(result.Data);
    }

    [HttpPost("notifications/{notificationId}/mark-read")]
    public async Task<IActionResult> MarkAsRead(string notificationId)
    {
        if (!Guid.TryParse(notificationId, out var id))
            return BadRequest(new { message = "Invalid notification id." });

        var result = await _notificationService.MarkAsReadAsync(CurrentUserId, id);
        return result.Success ? Ok(new { message = "Marked as read." }) : BadRequest();
    }

    [HttpPost("notifications/mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var result = await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return result.Success ? Ok(new { message = "All marked as read." }) : BadRequest();
    }
}
