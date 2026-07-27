using ElearningService.Common;
using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface IEnrollmentService
{
    Task<List<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId);
    Task<ApiResponse<EnrollmentDto>> EnrollAsync(Guid studentId, string courseId);
}

public class EnrollmentService : IEnrollmentService
{
    private readonly ElearningDbContext _db;

    public EnrollmentService(ElearningDbContext db) => _db = db;

    public async Task<List<EnrollmentDto>> GetMyEnrollmentsAsync(Guid studentId)
    {
        var enrollments = await _db.Enrollments
            .Where(e => e.StudentId == studentId)
            .Include(e => e.Course)
            .OrderByDescending(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return enrollments.Select(Map).ToList();
    }

    public async Task<ApiResponse<EnrollmentDto>> EnrollAsync(Guid studentId, string courseId)
    {
        if (!Guid.TryParse(courseId, out var id))
            return ApiResponse<EnrollmentDto>.Fail("Invalid course id.");

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return ApiResponse<EnrollmentDto>.Fail("Course not found.");

        var existing = await _db.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == id);

        if (existing is not null)
            return ApiResponse<EnrollmentDto>.Ok(Map(existing), "Already enrolled.");

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = id,
            Course = course,
            Status = "active"
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        return ApiResponse<EnrollmentDto>.Ok(Map(enrollment), "Enrolled.");
    }

    private static EnrollmentDto Map(Enrollment e) => new(
        Id: e.Id.ToString(),
        Student: e.StudentId.ToString(),
        Course: e.CourseId.ToString(),
        CourseTitle: e.Course?.Title ?? string.Empty,
        Status: e.Status,
        IsEnrolled: e.Status == "active",
        EnrolledAt: DateFormat.Iso(e.CreatedAt));
}
