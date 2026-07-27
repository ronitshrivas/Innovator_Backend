using System.Globalization;
using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface IPaymentService
{
    Task<ApiResponse<InitiatePaymentResponse>> InitiateAsync(Guid studentId, string courseId);
}

public class PaymentService : IPaymentService
{
    private readonly ElearningDbContext _db;
    private readonly IConfiguration _config;

    public PaymentService(ElearningDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ApiResponse<InitiatePaymentResponse>> InitiateAsync(Guid studentId, string courseId)
    {
        if (!Guid.TryParse(courseId, out var id))
            return ApiResponse<InitiatePaymentResponse>.Fail("Invalid course id.");

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return ApiResponse<InitiatePaymentResponse>.Fail("Course not found.");

        // Grant access on initiation. This service has no live payment-gateway
        // callback, so the enrollment is created here and the client unlocks the
        // course once it returns from the payment WebView. Swap this out for a
        // verified callback when a real Khalti/eSewa integration is wired up.
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == id);

        if (enrollment is null)
        {
            _db.Enrollments.Add(new Enrollment
            {
                StudentId = studentId,
                CourseId = id,
                Status = "active"
            });
        }
        else
        {
            enrollment.Status = "active";
        }

        _db.Notifications.Add(new Notification
        {
            UserId = studentId,
            Title = "Payment received",
            Message = $"You now have access to \"{course.Title}\".",
            NotificationType = "payment",
            DataType = "course",
            DataCourseId = course.Id.ToString()
        });

        await _db.SaveChangesAsync();

        var pidx = Guid.NewGuid().ToString("N");
        var baseUrl = _config["Khalti:BaseUrl"] ?? "https://khalti.com/pay";
        var paymentUrl = $"{baseUrl}?pidx={pidx}";

        var response = new InitiatePaymentResponse(
            Pidx: pidx,
            PaymentUrl: paymentUrl,
            CourseId: course.Id.ToString(),
            Amount: course.Price.ToString("0.00", CultureInfo.InvariantCulture),
            Status: "initiated");

        return ApiResponse<InitiatePaymentResponse>.Ok(response, "Payment initiated.");
    }
}
