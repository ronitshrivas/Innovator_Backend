using Microsoft.EntityFrameworkCore;
using ProfileService.Entities;

namespace ProfileService.Data;

public class ProfileDbContext : DbContext
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<SuggestionDismissal> SuggestionDismissals => Set<SuggestionDismissal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<UserProfile>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.AuthUserId).IsUnique();
            p.HasIndex(x => x.Username).IsUnique();
            p.HasIndex(x => x.Email).IsUnique();
            p.Property(x => x.Username).HasMaxLength(50).IsRequired();
            p.Property(x => x.FullName).HasMaxLength(150);
            p.Property(x => x.Email).HasMaxLength(255).IsRequired();
            p.Property(x => x.Bio).HasMaxLength(500);
            p.Property(x => x.InterestsJson).HasDefaultValue("[]");
        });

        builder.Entity<Follow>(f =>
        {
            f.HasKey(x => x.Id);
            f.HasIndex(x => new { x.FollowerId, x.FollowingId }).IsUnique();

            f.HasOne(x => x.Follower)
             .WithMany(u => u.Following)
             .HasForeignKey(x => x.FollowerId)
             .OnDelete(DeleteBehavior.Cascade);

            f.HasOne(x => x.FollowingUser)
             .WithMany(u => u.Followers)
             .HasForeignKey(x => x.FollowingId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSettings>(s =>
        {
            s.HasKey(x => x.Id);
            s.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<SuggestionDismissal>(d =>
        {
            d.HasKey(x => x.Id);
            d.HasIndex(x => new { x.UserId, x.DismissedUserId }).IsUnique();
        });

        builder.Entity<BlockedUser>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();

            b.HasOne(x => x.Blocker)
             .WithMany(u => u.BlockedUsers)
             .HasForeignKey(x => x.BlockerId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Blocked)
             .WithMany()
             .HasForeignKey(x => x.BlockedId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
