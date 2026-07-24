using Microsoft.EntityFrameworkCore;
using SearchService.Entities;

namespace SearchService.Data;

public class SearchDbContext : DbContext
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options) { }

    public DbSet<UserIndex> UserIndex => Set<UserIndex>();
    public DbSet<PostIndex> PostIndex => Set<PostIndex>();
    public DbSet<FollowGraph> FollowGraph => Set<FollowGraph>();
    public DbSet<SearchHistory> SearchHistory => Set<SearchHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<UserIndex>(u =>
        {
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.AuthUserId).IsUnique();
            u.HasIndex(x => x.Username);
            u.HasIndex(x => x.FullName);
            u.Property(x => x.Username).HasMaxLength(50);
            u.Property(x => x.FullName).HasMaxLength(150);
            u.Property(x => x.Bio).HasMaxLength(500);
        });

        builder.Entity<PostIndex>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.PostId).IsUnique();
            p.HasIndex(x => x.AuthorId);
            p.HasIndex(x => x.Content);
            p.Property(x => x.Content).HasMaxLength(5000);
        });

        builder.Entity<FollowGraph>(f =>
        {
            f.HasKey(x => x.Id);
            f.HasIndex(x => new { x.FollowerId, x.FollowingId }).IsUnique();
            f.HasIndex(x => x.FollowerId);
            f.HasIndex(x => x.FollowingId);
        });

        builder.Entity<SearchHistory>(s =>
        {
            s.HasKey(x => x.Id);
            s.HasIndex(x => x.UserId);
            s.Property(x => x.Query).HasMaxLength(200);
        });
    }
}
