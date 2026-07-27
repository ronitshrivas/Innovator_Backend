using System.ComponentModel.DataAnnotations;

namespace ElearningService.DTOs;

// ---- Courses ----

public record CourseContentDto(
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

public record CourseDto(
    string Id,
    string Vendor,
    string VendorName,
    string Category,
    string? CategoryName,
    string Title,
    string Description,
    string Price,
    string? Thumbnail,
    string CourseType,
    bool IsPublished,
    bool IsEnrolled,
    string CreatedAt,
    List<CourseContentDto> Contents
);

// ---- Enrollments ----

public record EnrollmentDto(
    string Id,
    string Student,
    string Course,
    string CourseTitle,
    string Status,
    bool IsEnrolled,
    string EnrolledAt
);

public record EnrollRequest(
    [Required] string Course
);

// ---- Payments ----

public record InitiatePaymentRequest(
    [Required] string CourseId
);

public record InitiatePaymentResponse(
    string Pidx,
    string PaymentUrl,
    string CourseId,
    string Amount,
    string Status
);

// ---- FCM tokens ----

public record FcmTokenRequest(
    [Required] string Token,
    string DeviceName = ""
);

public record FcmTokenResponse(
    string Id,
    string Token,
    string DeviceName
);

// ---- Notifications ----

public record NotificationDataDto(
    string Type,
    string CourseId
);

public record NotificationDto(
    string Id,
    string Title,
    string Message,
    string NotificationType,
    bool IsRead,
    string CreatedAt,
    NotificationDataDto Data
);
