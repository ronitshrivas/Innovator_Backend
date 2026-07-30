using ElearningService.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Data;

/// <summary>
/// Inserts a small set of published courses on first run so the course list
/// screen has something to show. Runs once — it no-ops if courses already exist.
/// </summary>
public static class ElearningSeeder
{
    public static async Task SeedAsync(ElearningDbContext db)
    {
        if (await db.Courses.AnyAsync())
            return;

        var programming = new Category { Name = "Programming", Slug = "programming" };
        var design = new Category { Name = "Design", Slug = "design" };
        db.Categories.AddRange(programming, design);

        var flutter = new Course
        {
            Vendor = "innovator-academy",
            VendorName = "Innovator Academy",
            Category = programming,
            Title = "Flutter for Beginners",
            Description = "Build your first cross-platform mobile app with Flutter and Dart.",
            Price = 0m,
            CourseType = "free",
            IsPublished = true,
            Thumbnail = null,
            Contents = new List<CourseContent>
            {
                new()
                {
                    Title = "Introduction to Flutter",
                    InstructorName = "Aashish Sharma",
                    VideoUrl = "https://www.youtube.com/embed/1gDhl4leEzA",
                    Duration = 620,
                    CourseLevel = "beginner",
                    IsPreview = true,
                    Order = 1
                },
                new()
                {
                    Title = "Widgets and Layouts",
                    InstructorName = "Aashish Sharma",
                    VideoUrl = "https://www.youtube.com/embed/RgqYcVVzL1E",
                    Duration = 845,
                    CourseLevel = "beginner",
                    IsPreview = false,
                    Order = 2
                }
            }
        };

        var dotnet = new Course
        {
            Vendor = "innovator-academy",
            VendorName = "Innovator Academy",
            Category = programming,
            Title = "REST APIs with ASP.NET Core",
            Description = "Design and build production-ready REST APIs using ASP.NET Core and EF Core.",
            Price = 1499m,
            CourseType = "paid",
            IsPublished = true,
            Thumbnail = null,
            Contents = new List<CourseContent>
            {
                new()
                {
                    Title = "Project Setup",
                    InstructorName = "Ronit Shrivastav",
                    VideoUrl = "https://www.youtube.com/embed/Bfk4N2GIfjM",
                    Duration = 540,
                    CourseLevel = "intermediate",
                    IsPreview = true,
                    Order = 1
                },
                new()
                {
                    Title = "Controllers and Routing",
                    InstructorName = "Ronit Shrivastav",
                    VideoUrl = "https://www.youtube.com/embed/hZ1DKfmZDzk",
                    Duration = 910,
                    CourseLevel = "intermediate",
                    IsPreview = false,
                    Order = 2
                }
            }
        };

        var uiux = new Course
        {
            Vendor = "innovator-studio",
            VendorName = "Innovator Studio",
            Category = design,
            Title = "UI/UX Design Fundamentals",
            Description = "Learn the principles of user-centered design and prototyping.",
            Price = 0m,
            CourseType = "free",
            IsPublished = true,
            Thumbnail = null,
            Contents = new List<CourseContent>
            {
                new()
                {
                    Title = "Design Thinking Basics",
                    InstructorName = "Priya Karki",
                    VideoUrl = "https://www.youtube.com/embed/6lmvCqvmjfE",
                    Duration = 700,
                    CourseLevel = "beginner",
                    IsPreview = true,
                    Order = 1
                }
            }
        };

        var uiuxs = new Course
        {
            Vendor = "innovator-studio",
            VendorName = "Innovator Studio",
            Category = design,
            Title = "UI/UX Design Fundamentals and Advance",
            Description = "Learn the principles of user-centered design and prototyping.",
            Price = 0m,
            CourseType = "free",
            IsPublished = true,
            Thumbnail = null,
            Contents = new List<CourseContent>
            {
                new()
                {
                    Title = "Design Thinking Basics",
                    InstructorName = "Priya Karki",
                    VideoUrl = "https://www.youtube.com/embed/6lmvCqvmjfE",
                    Duration = 700,
                    CourseLevel = "beginner",
                    IsPreview = true,
                    Order = 1
                }
            }
        };

        db.Courses.AddRange(flutter, dotnet, uiux);
        await db.SaveChangesAsync();
    }
}
