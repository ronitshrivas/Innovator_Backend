using System.Security.Claims;
using ElearningService.DTOs;
using ElearningService.Services;
using Innovator.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElearningService.Controllers;

/// <summary>
/// Shared course/content/enrollment management. Concrete controllers below fix
/// the caller's scope (admin = every vendor, vendor = only their own courses).
/// </summary>
public abstract class ManagementControllerBase : ControllerBase
{
    protected readonly IElearningAdminService Service;

    protected ManagementControllerBase(IElearningAdminService service) => Service = service;

    protected abstract bool IsAdminScope { get; }

    protected VendorScope Scope
    {
        get
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            var username = User.FindFirstValue("username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var userId = Guid.TryParse(idValue, out var id) ? id : Guid.Empty;
            return new VendorScope(IsAdminScope, userId, username);
        }
    }

    protected IActionResult Respond<T>(ApiResponse<T> result) =>
        result.Success ? Ok(result) : BadRequest(result);

    // ----- Dashboard -----

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() => Respond(await Service.GetDashboardAsync(Scope));

    // ----- Courses -----

    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? type,
        [FromQuery] bool? published) =>
        Respond(await Service.GetCoursesAsync(Scope, search, category, type, published));

    [HttpGet("courses/{id:guid}")]
    public async Task<IActionResult> GetCourse(Guid id) => Respond(await Service.GetCourseAsync(Scope, id));

    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request) =>
        Respond(await Service.CreateCourseAsync(Scope, request));

    [HttpPut("courses/{id:guid}")]
    public async Task<IActionResult> UpdateCourse(Guid id, [FromBody] UpdateCourseRequest request) =>
        Respond(await Service.UpdateCourseAsync(Scope, id, request));

    [HttpDelete("courses/{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id) => Respond(await Service.DeleteCourseAsync(Scope, id));

    [HttpPatch("courses/{id:guid}/published")]
    public async Task<IActionResult> SetPublished(Guid id, [FromBody] SetPublishedRequest request) =>
        Respond(await Service.SetPublishedAsync(Scope, id, request.IsPublished));

    [HttpPost("courses/{id:guid}/thumbnail")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SetThumbnail(Guid id, IFormFile file) =>
        Respond(await Service.SetThumbnailAsync(Scope, id, file));

    // ----- Contents (lessons) -----

    [HttpGet("courses/{courseId:guid}/contents")]
    public async Task<IActionResult> GetContents(Guid courseId) =>
        Respond(await Service.GetContentsAsync(Scope, courseId));

    [HttpPost("courses/{courseId:guid}/contents")]
    public async Task<IActionResult> AddContent(Guid courseId, [FromBody] CreateContentRequest request) =>
        Respond(await Service.AddContentAsync(Scope, courseId, request));

    [HttpPut("courses/{courseId:guid}/contents/{contentId:guid}")]
    public async Task<IActionResult> UpdateContent(Guid courseId, Guid contentId, [FromBody] UpdateContentRequest request) =>
        Respond(await Service.UpdateContentAsync(Scope, courseId, contentId, request));


    [HttpDelete("courses/{courseId:guid}/contents/{contentId:guid}")]
    public async Task<IActionResult> DeleteContent(Guid courseId, Guid contentId) =>
        Respond(await Service.DeleteContentAsync(Scope, courseId, contentId));

    [HttpPost("courses/{courseId:guid}/contents/{contentId:guid}/video")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadVideo(Guid courseId, Guid contentId, IFormFile file) =>
        Respond(await Service.UploadContentVideoAsync(Scope, courseId, contentId, file));

    [HttpPost("courses/{courseId:guid}/contents/{contentId:guid}/document")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(Guid courseId, Guid contentId, IFormFile file) =>
        Respond(await Service.UploadContentDocumentAsync(Scope, courseId, contentId, file));

    // ----- Enrollments -----

    [HttpGet("courses/{courseId:guid}/enrollments")]
    public async Task<IActionResult> GetEnrollments(Guid courseId) =>
        Respond(await Service.GetCourseEnrollmentsAsync(Scope, courseId));

    // ----- Categories (read) -----

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Respond(await Service.GetCategoriesAsync());
}

[ApiController]
[Route("api/vendor/elearning")]
[Authorize(Roles = "vendor,admin")]
public class VendorElearningController : ManagementControllerBase
{
    public VendorElearningController(IElearningAdminService service) : base(service) { }

    protected override bool IsAdminScope => false;
}

[ApiController]
[Route("api/admin/elearning")]
[Authorize(Roles = "admin")]
public class AdminElearningController : ManagementControllerBase
{
    public AdminElearningController(IElearningAdminService service) : base(service) { }

    protected override bool IsAdminScope => true;

    // ----- Categories (admin write) -----

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request) =>
        Respond(await Service.CreateCategoryAsync(request));

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request) =>
        Respond(await Service.UpdateCategoryAsync(id, request));

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id) =>
        Respond(await Service.DeleteCategoryAsync(id));

    // ----- Vendor summaries (computed from courses) -----

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors() => Respond(await Service.GetVendorsAsync());
}

// ---------------------------------------------------------------------------
// Vendor accounts: admin creates/manages them; vendors log in to get a token.
// ---------------------------------------------------------------------------

[ApiController]
[Route("api/admin/elearning/vendor-accounts")]
[Authorize(Roles = "admin")]
public class AdminVendorAccountController : ControllerBase
{
    private readonly IVendorService _vendors;

    public AdminVendorAccountController(IVendorService vendors) => _vendors = vendors;

    private IActionResult Respond<T>(ApiResponse<T> r) => r.Success ? Ok(r) : BadRequest(r);

    [HttpGet]
    public async Task<IActionResult> List() => Respond(await _vendors.ListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVendorRequest request) =>
        Respond(await _vendors.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVendorRequest request) =>
        Respond(await _vendors.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        Respond(await _vendors.DeleteAsync(id));
}

[ApiController]
[Route("api/vendor/auth")]
public class VendorAuthController : ControllerBase
{
    private readonly IVendorService _vendors;

    public VendorAuthController(IVendorService vendors) => _vendors = vendors;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] VendorLoginRequest request)
    {
        var result = await _vendors.LoginAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }
}

// Admin management of e-learning banners (image + linked course).
[ApiController]
[Route("api/admin/elearning/banners")]
[Authorize(Roles = "admin")]
public class AdminBannerController : ControllerBase
{
    private readonly IBannerService _banners;

    public AdminBannerController(IBannerService banners) => _banners = banners;

    private IActionResult Respond<T>(ApiResponse<T> result) =>
        result.Success ? Ok(result) : BadRequest(result);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Respond(await _banners.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBannerRequest request) =>
        Respond(await _banners.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBannerRequest request) =>
        Respond(await _banners.UpdateAsync(id, request));

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SetImage(Guid id, IFormFile image) =>
        Respond(await _banners.SetImageAsync(id, image));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        Respond(await _banners.DeleteAsync(id));
}
