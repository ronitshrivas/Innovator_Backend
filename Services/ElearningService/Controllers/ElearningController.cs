using System.Security.Claims;
using ElearningService.DTOs;
using ElearningService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElearningService.Controllers;

public abstract class ElearningControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    protected Guid? CurrentUserIdOrNull
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}

[ApiController]
[Route("api")]
public class CourseController : ElearningControllerBase
{
    private readonly ICourseService _courseService;

    public CourseController(ICourseService courseService) => _courseService = courseService;

    // Auth is optional: a signed-in student also gets is_enrolled flags.
    [HttpGet("courses")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCourses()
    {
        var courses = await _courseService.GetCoursesAsync(CurrentUserIdOrNull);
        return Ok(courses);
    }
}

// Public banners for the e-learning home carousel.
[ApiController]
[Route("api")]
public class BannerController : ControllerBase
{
    private readonly IBannerService _banners;

    public BannerController(IBannerService banners) => _banners = banners;

    [HttpGet("banners")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBanners()
    {
        var result = await _banners.GetActiveAsync();
        return Ok(result.Data);
    }
}

[ApiController]
[Route("api/student")]
[Authorize]
public class EnrollmentController : ElearningControllerBase
{
    private readonly IEnrollmentService _enrollmentService;

    public EnrollmentController(IEnrollmentService enrollmentService) =>
        _enrollmentService = enrollmentService;

    [HttpGet("enrollments")]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(CurrentUserId);
        return Ok(enrollments);
    }

    [HttpPost("enrollments")]
    public async Task<IActionResult> Enroll([FromBody] EnrollRequest request)
    {
        var result = await _enrollmentService.EnrollAsync(CurrentUserId, request.Course);
        return result.Success
            ? StatusCode(201, result.Data)
            : BadRequest(new { message = result.Message });
    }
}

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ElearningControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request)
    {
        var result = await _paymentService.InitiateAsync(CurrentUserId, request.CourseId);
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Message });
    }
}

[ApiController]
[Route("api/fcm-tokens")]
[Authorize]
public class FcmTokenController : ElearningControllerBase
{
    private readonly INotificationService _notificationService;

    public FcmTokenController(INotificationService notificationService) =>
        _notificationService = notificationService;

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] FcmTokenRequest request)
    {
        var response = await _notificationService.RegisterTokenAsync(CurrentUserId, request);
        return StatusCode(201, response);
    }

    [HttpPatch("{tokenId}")]
    public async Task<IActionResult> Update(string tokenId, [FromBody] FcmTokenRequest request)
    {
        var result = await _notificationService.UpdateTokenAsync(CurrentUserId, tokenId, request);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    [HttpDelete("{tokenId}")]
    public async Task<IActionResult> Delete(string tokenId)
    {
        var result = await _notificationService.DeleteTokenAsync(CurrentUserId, tokenId);
        return result.Success
            ? NoContent()
            : NotFound(new { message = result.Message });
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ElearningControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService) =>
        _notificationService = notificationService;

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = await _notificationService.GetNotificationsAsync(CurrentUserId);
        return Ok(notifications);
    }

    [HttpPost("{notificationId}/mark_as_read")]
    public async Task<IActionResult> MarkAsRead(string notificationId)
    {
        var result = await _notificationService.MarkAsReadAsync(CurrentUserId, notificationId);
        return result.Success
            ? Ok(new { message = "Marked as read." })
            : NotFound(new { message = result.Message });
    }

    [HttpPost("mark_all_as_read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Ok(new { message = "All marked as read." });
    }
}
