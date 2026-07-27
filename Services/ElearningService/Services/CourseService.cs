using System.Globalization;
using ElearningService.Common;
using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface ICourseService
{
    Task<List<CourseDto>> GetCoursesAsync(Guid? studentId);
}

public class CourseService : ICourseService
{
    private readonly ElearningDbContext _db;

    public CourseService(ElearningDbContext db) => _db = db;

    public async Task<List<CourseDto>> GetCoursesAsync(Guid? studentId)
    {
        var courses = await _db.Courses
            .Where(c => c.IsPublished)
            .Include(c => c.Category)
            .Include(c => c.Contents)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var enrolledCourseIds = studentId is null
            ? new HashSet<Guid>()
            : (await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.Status == "active")
                .Select(e => e.CourseId)
                .ToListAsync())
                .ToHashSet();

        return courses.Select(c => Map(c, enrolledCourseIds.Contains(c.Id))).ToList();
    }

    private static CourseDto Map(Course c, bool isEnrolled) => new(
        Id: c.Id.ToString(),
        Vendor: c.Vendor,
        VendorName: c.VendorName,
        Category: c.CategoryId?.ToString() ?? string.Empty,
        CategoryName: c.Category?.Name,
        Title: c.Title,
        Description: c.Description,
        Price: c.Price.ToString("0.00", CultureInfo.InvariantCulture),
        Thumbnail: c.Thumbnail,
        CourseType: c.CourseType,
        IsPublished: c.IsPublished,
        IsEnrolled: isEnrolled,
        CreatedAt: DateFormat.Iso(c.CreatedAt),
        Contents: c.Contents
            .OrderBy(x => x.Order)
            .Select(MapContent)
            .ToList());

    private static CourseContentDto MapContent(CourseContent x) => new(
        Id: x.Id.ToString(),
        Course: x.CourseId.ToString(),
        Title: x.Title,
        InstructorName: x.InstructorName,
        VideoUrl: x.VideoUrl,
        DirectVideoUrl: x.DirectVideoUrl,
        VideoFile: x.VideoFile,
        Thumbnail: x.Thumbnail,
        Duration: x.Duration,
        DocumentUrl: x.DocumentUrl,
        DocumentFile: x.DocumentFile,
        CourseLevel: x.CourseLevel,
        IsPreview: x.IsPreview,
        Order: x.Order,
        CreatedAt: DateFormat.Iso(x.CreatedAt));
}
