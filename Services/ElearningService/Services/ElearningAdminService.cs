using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface IElearningAdminService
{
    // Courses
    Task<ApiResponse<List<ManageCourseDto>>> GetCoursesAsync(VendorScope scope, string? search, string? category, string? type, bool? published);
    Task<ApiResponse<ManageCourseDto>> GetCourseAsync(VendorScope scope, Guid id);
    Task<ApiResponse<ManageCourseDto>> CreateCourseAsync(VendorScope scope, CreateCourseRequest request);
    Task<ApiResponse<ManageCourseDto>> UpdateCourseAsync(VendorScope scope, Guid id, UpdateCourseRequest request);
    Task<ApiResponse<bool>> DeleteCourseAsync(VendorScope scope, Guid id);
    Task<ApiResponse<ManageCourseDto>> SetPublishedAsync(VendorScope scope, Guid id, bool isPublished);
    Task<ApiResponse<ManageCourseDto>> SetThumbnailAsync(VendorScope scope, Guid id, IFormFile file);

    // Contents
    Task<ApiResponse<List<ManageContentDto>>> GetContentsAsync(VendorScope scope, Guid courseId);
    Task<ApiResponse<ManageContentDto>> AddContentAsync(VendorScope scope, Guid courseId, CreateContentRequest request);
    Task<ApiResponse<ManageContentDto>> UpdateContentAsync(VendorScope scope, Guid courseId, Guid contentId, UpdateContentRequest request);
    Task<ApiResponse<bool>> DeleteContentAsync(VendorScope scope, Guid courseId, Guid contentId);
    Task<ApiResponse<ManageContentDto>> UploadContentVideoAsync(VendorScope scope, Guid courseId, Guid contentId, IFormFile file);
    Task<ApiResponse<ManageContentDto>> UploadContentDocumentAsync(VendorScope scope, Guid courseId, Guid contentId, IFormFile file);

    // Enrollments
    Task<ApiResponse<List<ManageEnrollmentDto>>> GetCourseEnrollmentsAsync(VendorScope scope, Guid courseId);

    // Categories
    Task<ApiResponse<List<ManageCategoryDto>>> GetCategoriesAsync();
    Task<ApiResponse<ManageCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request);
    Task<ApiResponse<ManageCategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request);
    Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id);

    // Admin-only
    Task<ApiResponse<List<VendorSummaryDto>>> GetVendorsAsync();

    // Dashboard
    Task<ApiResponse<ManageDashboardDto>> GetDashboardAsync(VendorScope scope);
}

public class ElearningAdminService : IElearningAdminService
{
    private readonly ElearningDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ElearningAdminService(ElearningDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    // ---------- Courses ----------

    public async Task<ApiResponse<List<ManageCourseDto>>> GetCoursesAsync(
        VendorScope scope, string? search, string? category, string? type, bool? published)
    {
        var query = _db.Courses
            .Include(c => c.Category)
            .Include(c => c.Contents)
            .Include(c => c.Enrollments)
            .AsQueryable();

        if (!scope.IsAdmin)
            query = query.Where(c => c.Vendor == scope.UserId.ToString());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term) ||
                                     c.Description.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category) && Guid.TryParse(category, out var catId))
            query = query.Where(c => c.CategoryId == catId);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(c => c.CourseType == type);

        if (published.HasValue)
            query = query.Where(c => c.IsPublished == published.Value);

        var courses = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return ApiResponse<List<ManageCourseDto>>.Ok(courses.Select(MapCourse).ToList());
    }

    public async Task<ApiResponse<ManageCourseDto>> GetCourseAsync(VendorScope scope, Guid id)
    {
        var course = await LoadCourseAsync(id);
        if (course is null)
            return ApiResponse<ManageCourseDto>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<ManageCourseDto>.Fail("You do not have access to this course.");

        return ApiResponse<ManageCourseDto>.Ok(MapCourse(course));
    }

    public async Task<ApiResponse<ManageCourseDto>> CreateCourseAsync(VendorScope scope, CreateCourseRequest request)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(request.CategoryId))
        {
            if (!Guid.TryParse(request.CategoryId, out var cid))
                return ApiResponse<ManageCourseDto>.Fail("Invalid category id.");
            if (!await _db.Categories.AnyAsync(c => c.Id == cid))
                return ApiResponse<ManageCourseDto>.Fail("Category not found.");
            categoryId = cid;
        }

        // Vendor always owns what they create. Admins may assign to a vendor,
        // otherwise the course is owned by the admin's own account.
        var vendorId = scope.UserId.ToString();
        var vendorName = scope.Username;
        if (scope.IsAdmin && !string.IsNullOrWhiteSpace(request.VendorId))
        {
            vendorId = request.VendorId;
            vendorName = string.IsNullOrWhiteSpace(request.VendorName) ? request.VendorId : request.VendorName;
        }

        var course = new Course
        {
            Vendor = vendorId,
            VendorName = vendorName,
            CategoryId = categoryId,
            Title = request.Title,
            Description = request.Description,
            Price = request.CourseType == "paid" ? request.Price : 0,
            CourseType = request.CourseType,
            IsPublished = request.IsPublished,
            Thumbnail = request.Thumbnail
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        return ApiResponse<ManageCourseDto>.Ok(MapCourse((await LoadCourseAsync(course.Id))!), "Course created.");
    }

    public async Task<ApiResponse<ManageCourseDto>> UpdateCourseAsync(VendorScope scope, Guid id, UpdateCourseRequest request)
    {
        var course = await LoadCourseAsync(id);
        if (course is null)
            return ApiResponse<ManageCourseDto>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<ManageCourseDto>.Fail("You do not have access to this course.");

        if (request.Title is not null) course.Title = request.Title;
        if (request.Description is not null) course.Description = request.Description;
        if (request.CourseType is not null) course.CourseType = request.CourseType;
        if (request.Price.HasValue) course.Price = request.Price.Value;
        if (course.CourseType == "free") course.Price = 0;
        if (request.IsPublished.HasValue) course.IsPublished = request.IsPublished.Value;
        if (request.Thumbnail is not null) course.Thumbnail = request.Thumbnail;

        if (request.CategoryId is not null)
        {
            if (request.CategoryId.Length == 0)
            {
                course.CategoryId = null;
            }
            else
            {
                if (!Guid.TryParse(request.CategoryId, out var cid))
                    return ApiResponse<ManageCourseDto>.Fail("Invalid category id.");
                if (!await _db.Categories.AnyAsync(c => c.Id == cid))
                    return ApiResponse<ManageCourseDto>.Fail("Category not found.");
                course.CategoryId = cid;
            }
        }

        course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<ManageCourseDto>.Ok(MapCourse((await LoadCourseAsync(id))!), "Course updated.");
    }

    public async Task<ApiResponse<bool>> DeleteCourseAsync(VendorScope scope, Guid id)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return ApiResponse<bool>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<bool>.Fail("You do not have access to this course.");

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Course deleted.");
    }

    public async Task<ApiResponse<ManageCourseDto>> SetPublishedAsync(VendorScope scope, Guid id, bool isPublished)
    {
        var course = await LoadCourseAsync(id);
        if (course is null)
            return ApiResponse<ManageCourseDto>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<ManageCourseDto>.Fail("You do not have access to this course.");

        course.IsPublished = isPublished;
        course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<ManageCourseDto>.Ok(MapCourse(course));
    }

    public async Task<ApiResponse<ManageCourseDto>> SetThumbnailAsync(VendorScope scope, Guid id, IFormFile file)
    {
        var course = await LoadCourseAsync(id);
        if (course is null)
            return ApiResponse<ManageCourseDto>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<ManageCourseDto>.Fail("You do not have access to this course.");

        course.Thumbnail = await SaveFileAsync(file, "courses");
        course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<ManageCourseDto>.Ok(MapCourse(course), "Thumbnail updated.");
    }

    // ---------- Contents ----------

    public async Task<ApiResponse<List<ManageContentDto>>> GetContentsAsync(VendorScope scope, Guid courseId)
    {
        var course = await LoadCourseAsync(courseId);
        if (course is null)
            return ApiResponse<List<ManageContentDto>>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<List<ManageContentDto>>.Fail("You do not have access to this course.");

        var contents = course.Contents.OrderBy(c => c.Order).Select(MapContent).ToList();
        return ApiResponse<List<ManageContentDto>>.Ok(contents);
    }

    public async Task<ApiResponse<ManageContentDto>> AddContentAsync(VendorScope scope, Guid courseId, CreateContentRequest request)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
            return ApiResponse<ManageContentDto>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<ManageContentDto>.Fail("You do not have access to this course.");

        var content = new CourseContent
        {
            CourseId = courseId,
            Title = request.Title,
            InstructorName = request.InstructorName,
            VideoUrl = request.VideoUrl,
            DirectVideoUrl = request.DirectVideoUrl,
            Duration = request.Duration,
            DocumentUrl = request.DocumentUrl,
            CourseLevel = request.CourseLevel,
            IsPreview = request.IsPreview,
            Order = request.Order
        };

        _db.CourseContents.Add(content);
        await _db.SaveChangesAsync();
        return ApiResponse<ManageContentDto>.Ok(MapContent(content), "Content added.");
    }

    public async Task<ApiResponse<ManageContentDto>> UpdateContentAsync(VendorScope scope, Guid courseId, Guid contentId, UpdateContentRequest request)
    {
        var content = await LoadContentAsync(scope, courseId, contentId);
        if (content.Error is not null)
            return ApiResponse<ManageContentDto>.Fail(content.Error);

        var c = content.Content!;
        if (request.Title is not null) c.Title = request.Title;
        if (request.InstructorName is not null) c.InstructorName = request.InstructorName;
        if (request.VideoUrl is not null) c.VideoUrl = request.VideoUrl;
        if (request.DirectVideoUrl is not null) c.DirectVideoUrl = request.DirectVideoUrl;
        if (request.Duration.HasValue) c.Duration = request.Duration.Value;
        if (request.DocumentUrl is not null) c.DocumentUrl = request.DocumentUrl;
        if (request.CourseLevel is not null) c.CourseLevel = request.CourseLevel;
        if (request.IsPreview.HasValue) c.IsPreview = request.IsPreview.Value;
        if (request.Order.HasValue) c.Order = request.Order.Value;

        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<ManageContentDto>.Ok(MapContent(c), "Content updated.");
    }

    public async Task<ApiResponse<bool>> DeleteContentAsync(VendorScope scope, Guid courseId, Guid contentId)
    {
        var content = await LoadContentAsync(scope, courseId, contentId);
        if (content.Error is not null)
            return ApiResponse<bool>.Fail(content.Error);

        _db.CourseContents.Remove(content.Content!);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Content deleted.");
    }

    public async Task<ApiResponse<ManageContentDto>> UploadContentVideoAsync(VendorScope scope, Guid courseId, Guid contentId, IFormFile file)
    {
        var content = await LoadContentAsync(scope, courseId, contentId);
        if (content.Error is not null)
            return ApiResponse<ManageContentDto>.Fail(content.Error);

        content.Content!.VideoFile = await SaveFileAsync(file, "videos");
        content.Content.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<ManageContentDto>.Ok(MapContent(content.Content), "Video uploaded.");
    }

    public async Task<ApiResponse<ManageContentDto>> UploadContentDocumentAsync(VendorScope scope, Guid courseId, Guid contentId, IFormFile file)
    {
        var content = await LoadContentAsync(scope, courseId, contentId);
        if (content.Error is not null)
            return ApiResponse<ManageContentDto>.Fail(content.Error);

        content.Content!.DocumentFile = await SaveFileAsync(file, "documents");
        content.Content.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<ManageContentDto>.Ok(MapContent(content.Content), "Document uploaded.");
    }

    // ---------- Enrollments ----------

    public async Task<ApiResponse<List<ManageEnrollmentDto>>> GetCourseEnrollmentsAsync(VendorScope scope, Guid courseId)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
            return ApiResponse<List<ManageEnrollmentDto>>.Fail("Course not found.");
        if (!scope.CanManage(course.Vendor))
            return ApiResponse<List<ManageEnrollmentDto>>.Fail("You do not have access to this course.");

        var enrollments = await _db.Enrollments
            .Where(e => e.CourseId == courseId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var result = enrollments.Select(e => new ManageEnrollmentDto(
            e.Id.ToString(),
            e.StudentId.ToString(),
            e.CourseId.ToString(),
            course.Title,
            e.Status,
            Iso(e.CreatedAt))).ToList();

        return ApiResponse<List<ManageEnrollmentDto>>.Ok(result);
    }

    // ---------- Categories ----------

    public async Task<ApiResponse<List<ManageCategoryDto>>> GetCategoriesAsync()
    {
        var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();

        var counts = await _db.Courses
            .Where(c => c.CategoryId != null)
            .GroupBy(c => c.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = counts.ToDictionary(x => x.CategoryId, x => x.Count);

        var result = categories
            .Select(c => MapCategory(c, countMap.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();

        return ApiResponse<List<ManageCategoryDto>>.Ok(result);
    }

    public async Task<ApiResponse<ManageCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(request.Name) : Slugify(request.Slug);

        if (await _db.Categories.AnyAsync(c => c.Slug == slug))
            return ApiResponse<ManageCategoryDto>.Fail("A category with this slug already exists.");

        var category = new Category { Name = request.Name, Slug = slug };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return ApiResponse<ManageCategoryDto>.Ok(MapCategory(category, 0), "Category created.");
    }

    public async Task<ApiResponse<ManageCategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
            return ApiResponse<ManageCategoryDto>.Fail("Category not found.");

        if (request.Name is not null) category.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            var slug = Slugify(request.Slug);
            if (await _db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id))
                return ApiResponse<ManageCategoryDto>.Fail("A category with this slug already exists.");
            category.Slug = slug;
        }

        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var count = await _db.Courses.CountAsync(c => c.CategoryId == id);
        return ApiResponse<ManageCategoryDto>.Ok(MapCategory(category, count), "Category updated.");
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
            return ApiResponse<bool>.Fail("Category not found.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Category deleted.");
    }

    // ---------- Vendors ----------

    public async Task<ApiResponse<List<VendorSummaryDto>>> GetVendorsAsync()
    {
        var courses = await _db.Courses
            .Include(c => c.Enrollments)
            .ToListAsync();

        var vendors = courses
            .GroupBy(c => new { c.Vendor, c.VendorName })
            .Select(g => new VendorSummaryDto(
                g.Key.Vendor,
                g.Key.VendorName,
                g.Count(),
                g.Count(c => c.IsPublished),
                g.Sum(c => c.Enrollments.Count)))
            .OrderByDescending(v => v.CourseCount)
            .ToList();

        return ApiResponse<List<VendorSummaryDto>>.Ok(vendors);
    }

    // ---------- Dashboard ----------

    public async Task<ApiResponse<ManageDashboardDto>> GetDashboardAsync(VendorScope scope)
    {
        var query = _db.Courses
            .Include(c => c.Category)
            .Include(c => c.Contents)
            .Include(c => c.Enrollments)
            .AsQueryable();

        if (!scope.IsAdmin)
            query = query.Where(c => c.Vendor == scope.UserId.ToString());

        var courses = await query.ToListAsync();

        var totalCourses = courses.Count;
        var published = courses.Count(c => c.IsPublished);
        var enrollments = courses.SelectMany(c => c.Enrollments).ToList();
        var totalEnrollments = enrollments.Count;
        var totalStudents = enrollments.Select(e => e.StudentId).Distinct().Count();
        var totalRevenue = courses
            .Where(c => c.CourseType == "paid")
            .Sum(c => c.Price * c.Enrollments.Count);

        var recent = courses
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(MapCourse)
            .ToList();

        var dashboard = new ManageDashboardDto(
            totalCourses,
            published,
            totalCourses - published,
            totalEnrollments,
            totalStudents,
            totalRevenue,
            recent);

        return ApiResponse<ManageDashboardDto>.Ok(dashboard);
    }

    // ---------- Helpers ----------

    private Task<Course?> LoadCourseAsync(Guid id) =>
        _db.Courses
            .Include(c => c.Category)
            .Include(c => c.Contents)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

    private async Task<(CourseContent? Content, string? Error)> LoadContentAsync(VendorScope scope, Guid courseId, Guid contentId)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
            return (null, "Course not found.");
        if (!scope.CanManage(course.Vendor))
            return (null, "You do not have access to this course.");

        var content = await _db.CourseContents.FirstOrDefaultAsync(c => c.Id == contentId && c.CourseId == courseId);
        if (content is null)
            return (null, "Content not found.");

        return (content, null);
    }

    private async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

        var targetDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8017").TrimEnd('/');
        return $"{baseUrl}/uploads/{folder}/{fileName}";
    }

    private ManageCourseDto MapCourse(Course c) => new(
        c.Id.ToString(),
        c.Vendor,
        c.VendorName,
        c.CategoryId?.ToString(),
        c.Category?.Name,
        c.Title,
        c.Description,
        c.Price,
        c.CourseType,
        c.IsPublished,
        ResolveUrl(c.Thumbnail),
        c.Contents.Count,
        c.Enrollments.Count,
        Iso(c.CreatedAt));

    private ManageContentDto MapContent(CourseContent x) => new(
        x.Id.ToString(),
        x.CourseId.ToString(),
        x.Title,
        x.InstructorName,
        x.VideoUrl,
        x.DirectVideoUrl,
        ResolveUrl(x.VideoFile),
        ResolveUrl(x.Thumbnail),
        x.Duration,
        x.DocumentUrl,
        ResolveUrl(x.DocumentFile),
        x.CourseLevel,
        x.IsPreview,
        x.Order,
        Iso(x.CreatedAt));

    private static ManageCategoryDto MapCategory(Category c, int courseCount) => new(
        c.Id.ToString(),
        c.Name,
        c.Slug,
        courseCount,
        Iso(c.CreatedAt));

    private string? ResolveUrl(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        var baseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:8017").TrimEnd('/');
        return $"{baseUrl}{path}";
    }

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string Slugify(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-');
    }
}
