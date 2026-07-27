using ElearningService.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Data;

public class ElearningDbContext : DbContext
{
    public ElearningDbContext(DbContextOptions<ElearningDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseContent> CourseContents => Set<CourseContent>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<FcmToken> FcmTokens => Set<FcmToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Course>(entity =>
        {
            entity.Property(c => c.Price).HasColumnType("numeric(10,2)");

            entity.HasOne(c => c.Category)
                .WithMany(cat => cat.Courses)
                .HasForeignKey(c => c.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CourseContent>(entity =>
        {
            entity.HasOne(cc => cc.Course)
                .WithMany(c => c.Contents)
                .HasForeignKey(cc => cc.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();
        });

        modelBuilder.Entity<FcmToken>()
            .HasIndex(t => t.Token);
    }
}
