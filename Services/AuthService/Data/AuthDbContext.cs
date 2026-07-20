using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.HasIndex(x => x.Username).IsUnique();
            u.Property(x => x.Username).HasMaxLength(50).IsRequired();
            u.Property(x => x.Email).HasMaxLength(255).IsRequired();
            u.Property(x => x.PasswordHash).IsRequired();
            u.Property(x => x.Role).HasMaxLength(30).HasDefaultValue("innovator");
        });

        builder.Entity<RefreshToken>(rt =>
        {
            rt.HasKey(x => x.Id);
            rt.HasOne(x => x.User)
              .WithMany(u => u.RefreshTokens)
              .HasForeignKey(x => x.UserId)
              .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OtpRecord>(o =>
        {
            o.HasKey(x => x.Id);
            o.HasOne(x => x.User)
             .WithMany(u => u.OtpRecords)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
