using Innovator.Shared.Entities;

namespace ElearningService.Entities;

// A vendor is an e-learning content provider account the admin creates and that
// can log in to manage its own courses. The vendor's Id (as a string) is stored
// on each Course.Vendor so ownership checks in VendorScope keep working.
public class Vendor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<Course> Courses { get; set; } = new();
}

public class Course : BaseEntity
{
    public string Vendor { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>"free" or "paid".</summary>
    public string CourseType { get; set; } = "free";

    public bool IsPublished { get; set; } = true;
    public string? Thumbnail { get; set; }

    public List<CourseContent> Contents { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();
}

public class CourseContent : BaseEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }
    public string? DirectVideoUrl { get; set; }
    public string? VideoFile { get; set; }
    public string? Thumbnail { get; set; }

    public double Duration { get; set; }

    public string? DocumentUrl { get; set; }
    public string? DocumentFile { get; set; }

    public string CourseLevel { get; set; } = "beginner";
    public bool IsPreview { get; set; }
    public int Order { get; set; }
}

public class Enrollment : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string Status { get; set; } = "active";
}

public class FcmToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = "course";
    public bool IsRead { get; set; }

    /// <summary>Payload surfaced under the response "data" object.</summary>
    public string DataType { get; set; } = "course";
    public string DataCourseId { get; set; } = string.Empty;
}
