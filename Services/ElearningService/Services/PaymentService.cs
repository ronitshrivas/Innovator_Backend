using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly IHttpClientFactory _httpFactory;

    public PaymentService(ElearningDbContext db, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _db = db;
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task<ApiResponse<InitiatePaymentResponse>> InitiateAsync(Guid studentId, string courseId)
    {
        if (!Guid.TryParse(courseId, out var id))
            return ApiResponse<InitiatePaymentResponse>.Fail("Invalid course id.");

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return ApiResponse<InitiatePaymentResponse>.Fail("Course not found.");

        // Free course: enrol immediately, no payment needed.
        if (course.Price <= 0 || course.CourseType.ToLower() == "free")
        {
            await EnrollAsync(studentId, course);
            return ApiResponse<InitiatePaymentResponse>.Ok(new InitiatePaymentResponse(
                Pidx: string.Empty,
                PaymentUrl: string.Empty,
                CourseId: course.Id.ToString(),
                Amount: "0.00",
                Status: "enrolled"), "Enrolled.");
        }

        var apiUrl = (_config["Khalti:ApiUrl"] ?? "https://khalti.com/api/v2").TrimEnd('/');
        var secret = _config["Khalti:SecretKey"] ?? string.Empty;
        var returnUrl = _config["Khalti:ReturnUrl"] ?? "http://36.253.137.34:8003/api/payments/khalti/callback";
        var websiteUrl = _config["Khalti:WebsiteUrl"] ?? "http://36.253.137.34:8003";
        var amountPaisa = (int)Math.Round(course.Price * 100m);

        var payload = new
        {
            return_url = returnUrl,
            website_url = websiteUrl,
            amount = amountPaisa,
            purchase_order_id = course.Id.ToString(),
            purchase_order_name = course.Title,
        };

        try
        {
            var client = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/epayment/initiate/")
            {
                Content = JsonContent.Create(payload),
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Key {secret}");

            var http = await client.SendAsync(req);
            var body = await http.Content.ReadAsStringAsync();
            if (!http.IsSuccessStatusCode)
                return ApiResponse<InitiatePaymentResponse>.Fail($"Khalti error: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var pidx = root.TryGetProperty("pidx", out var p) ? p.GetString() ?? "" : "";
            var paymentUrl = root.TryGetProperty("payment_url", out var u) ? u.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(paymentUrl))
                return ApiResponse<InitiatePaymentResponse>.Fail("Khalti did not return a payment URL.");

            return ApiResponse<InitiatePaymentResponse>.Ok(new InitiatePaymentResponse(
                Pidx: pidx,
                PaymentUrl: paymentUrl,
                CourseId: course.Id.ToString(),
                Amount: course.Price.ToString("0.00", CultureInfo.InvariantCulture),
                Status: "initiated"), "Payment initiated.");
        }
        catch (Exception ex)
        {
            return ApiResponse<InitiatePaymentResponse>.Fail($"Payment initiation failed: {ex.Message}");
        }
    }

    private async Task EnrollAsync(Guid studentId, Course course)
    {
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == course.Id);
        if (enrollment is null)
            _db.Enrollments.Add(new Enrollment
            {
                StudentId = studentId,
                CourseId = course.Id,
                Status = "active"
            });
        else
            enrollment.Status = "active";

        _db.Notifications.Add(new Notification
        {
            UserId = studentId,
            Title = "Enrolled",
            Message = $"You now have access to \"{course.Title}\".",
            NotificationType = "enrollment",
            DataType = "course",
            DataCourseId = course.Id.ToString()
        });
        await _db.SaveChangesAsync();
    }
}
