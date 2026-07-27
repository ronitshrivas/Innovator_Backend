using System.ComponentModel.DataAnnotations;

namespace ElearningService.DTOs;

/// <summary>
/// Describes who is acting. Admins see and manage every vendor's courses;
/// vendors are restricted to the courses they own.
/// </summary>
public record VendorScope(bool IsAdmin, Guid UserId, string Username)
{
    public bool CanManage(string courseVendor) =>
        IsAdmin || courseVendor == UserId.ToString();
}

// ---------- Courses ----------

public record ManageCourseDto(
    string Id,
    string Vendor,
    string VendorName,
    string? CategoryId,
    string? CategoryName,
    string Title,
    string Description,
    decimal Price,
    string CourseType,
    bool IsPublished,
    string? Thumbnail,
    int ContentCount,
    int EnrollmentCount,
    string CreatedAt
);

public record CreateCourseRequest(
    [Required, MaxLength(255)] string Title,
    string Description = "",
    [Range(0, double.MaxValue)] decimal Price = 0,
    string CourseType = "free",
    string? CategoryId = null,
    bool IsPublished = true,
    string? Thumbnail = null,
    // Admin-only: assign the course to a specific vendor. Ignored for vendors.
    string? VendorId = null,
    string? VendorName = null
);

public record UpdateCourseRequest(
    string? Title,
    string? Description,
    decimal? Price,
    string? CourseType,
    string? CategoryId,
    bool? IsPublished,
    string? Thumbnail
);

public record SetPublishedRequest(bool IsPublished);

// ---------- Course contents (lessons) ----------

public record ManageContentDto(
    string Id,
    string Course,
    string Title,
    string InstructorName,
    string? VideoUrl,
    string? DirectVideoUrl,
    string? VideoFile,
    string? Thumbnail,
    double Duration,
    string? DocumentUrl,
    string? DocumentFile,
    string CourseLevel,
    bool IsPreview,
    int Order,
    string CreatedAt
);

public record CreateContentRequest(
    [Required, MaxLength(255)] string Title,
    string InstructorName = "",
    string? VideoUrl = null,
    string? DirectVideoUrl = null,
    double Duration = 0,
    string? DocumentUrl = null,
    string CourseLevel = "beginner",
    bool IsPreview = false,
    int Order = 0
);

public record UpdateContentRequest(
    string? Title,
    string? InstructorName,
    string? VideoUrl,
    string? DirectVideoUrl,
    double? Duration,
    string? DocumentUrl,
    string? CourseLevel,
    bool? IsPreview,
    int? Order
);

// ---------- Categories ----------

public record ManageCategoryDto(
    string Id,
    string Name,
    string Slug,
    int CourseCount,
    string CreatedAt
);

public record CreateCategoryRequest(
    [Required, MaxLength(100)] string Name,
    string? Slug
);

public record UpdateCategoryRequest(
    string? Name,
    string? Slug
);

// ---------- Enrollments ----------

public record ManageEnrollmentDto(
    string Id,
    string StudentId,
    string CourseId,
    string CourseTitle,
    string Status,
    string EnrolledAt
);

// ---------- Vendors (admin) ----------

public record VendorSummaryDto(
    string VendorId,
    string VendorName,
    int CourseCount,
    int PublishedCount,
    int EnrollmentCount
);

// ---------- Dashboard ----------

public record ManageDashboardDto(
    int TotalCourses,
    int PublishedCourses,
    int DraftCourses,
    int TotalEnrollments,
    int TotalStudents,
    decimal TotalRevenue,
    List<ManageCourseDto> RecentCourses
);
