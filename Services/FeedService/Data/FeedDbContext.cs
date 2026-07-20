using FeedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Data;

public class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PostCategory> PostCategories => Set<PostCategory>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Post>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.AuthorId);
            p.HasIndex(x => x.CreatedAt);
            p.HasIndex(x => x.IsReel);
            p.Property(x => x.Content).HasMaxLength(5000);
            p.Property(x => x.Type).HasMaxLength(50).HasDefaultValue("post");

            p.HasOne(x => x.SharedPost)
             .WithMany(x => x.Reposts)
             .HasForeignKey(x => x.SharedPostId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PostMedia>(m =>
        {
            m.HasKey(x => x.Id);
            m.HasOne(x => x.Post)
             .WithMany(p => p.Media)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PostCategory>(pc =>
        {
            pc.HasKey(x => new { x.PostId, x.CategoryId });
            pc.HasOne(x => x.Post)
              .WithMany(p => p.Categories)
              .HasForeignKey(x => x.PostId)
              .OnDelete(DeleteBehavior.Cascade);
            pc.HasOne(x => x.Category)
              .WithMany(c => c.Posts)
              .HasForeignKey(x => x.CategoryId)
              .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Reaction>(r =>
        {
            r.HasKey(x => x.Id);
            r.HasIndex(x => new { x.PostId, x.AuthorId }).IsUnique();
            r.HasOne(x => x.Post)
             .WithMany(p => p.Reactions)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Comment>(c =>
        {
            c.HasKey(x => x.Id);
            c.HasIndex(x => x.PostId);
            c.Property(x => x.Content).HasMaxLength(2000);

            c.HasOne(x => x.Post)
             .WithMany(p => p.Comments)
             .HasForeignKey(x => x.PostId)
             .OnDelete(DeleteBehavior.Cascade);

            c.HasOne(x => x.Parent)
             .WithMany(x => x.Replies)
             .HasForeignKey(x => x.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Category>().HasData(
            new Category { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Technology", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Category { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Business", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Category { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Science", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Category { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Education", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Category { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Innovation", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
    }
}
