using System.Security.Claims;
using FeedService.DTOs;
using FeedService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedService.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationController(INotificationService notifications) =>
        _notifications = notifications;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    // Bare array — matches the app's poller (accepts list or { results }).
    // Trailing slashes are normalised globally so /api/notifications/ works too.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _notifications.GetForUserAsync(CurrentUserId);
        return Ok(items);
    }

    [HttpPost("{notificationId}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(string notificationId)
    {
        var result = await _notifications.MarkAsReadAsync(CurrentUserId, notificationId);
        return result.Success
            ? Ok(new { message = "Marked as read." })
            : NotFound(new { message = result.Message });
    }

    [HttpPost("mark-all-as-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notifications.MarkAllAsReadAsync(CurrentUserId);
        return Ok(new { message = "All marked as read." });
    }

    // The list screen may DELETE a notification; treat delete as mark-read
    // (no destructive delete endpoint needed for the activity feed).
    [HttpDelete("{notificationId}")]
    public async Task<IActionResult> Delete(string notificationId)
    {
        await _notifications.MarkAsReadAsync(CurrentUserId, notificationId);
        return NoContent();
    }
}

[ApiController]
[Route("api/fcm-tokens")]
[Authorize]
public class FeedFcmTokenController : ControllerBase
{
    private readonly INotificationService _notifications;

    public FeedFcmTokenController(INotificationService notifications) =>
        _notifications = notifications;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] FcmTokenRequest request)
    {
        var response = await _notifications.RegisterTokenAsync(CurrentUserId, request);
        return StatusCode(201, response);
    }

    // The app also PATCHes an existing token id; treat it as an upsert.
    [HttpPatch("{tokenId}")]
    public async Task<IActionResult> Update(string tokenId, [FromBody] FcmTokenRequest request)
    {
        var response = await _notifications.RegisterTokenAsync(CurrentUserId, request);
        return Ok(response);
    }

    [HttpDelete("{tokenId}")]
    public async Task<IActionResult> Delete(string tokenId)
    {
        var result = await _notifications.DeleteTokenAsync(CurrentUserId, tokenId);
        return result.Success ? NoContent() : NotFound(new { message = result.Message });
    }
}

// Called by other services (or internally) to raise a social notification.
[ApiController]
[Route("api/internal/notifications")]
public class InternalNotificationController : ControllerBase
{
    private readonly INotificationService _notifications;

    public InternalNotificationController(INotificationService notifications) =>
        _notifications = notifications;

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
    {
        await _notifications.CreateAsync(request);
        return Ok(new { message = "Notification created." });
    }
}
