using EcommerceService.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Data;

public class EcommerceDbContext : DbContext
{
    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options) { }
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentQr> PaymentQrs => Set<PaymentQr>();
    public DbSet<FcmToken> FcmTokens => Set<FcmToken>();
    public DbSet<EcommerceNotification> Notifications => Set<EcommerceNotification>();
    public DbSet<Banner> Banners => Set<Banner>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ProductCategory>(c =>
        {
            c.HasKey(x => x.Id);
            c.HasIndex(x => x.Slug).IsUnique();
            c.Property(x => x.Name).HasMaxLength(100).IsRequired();
            c.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Product>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.IsActive);
            p.Property(x => x.Name).HasMaxLength(255).IsRequired();
            p.Property(x => x.Price).HasPrecision(12, 2);

            p.HasOne(x => x.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            p.HasMany(x => x.Images)
             .WithOne(i => i.Product)
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Cart>(c =>
        {
            c.HasKey(x => x.Id);
            c.HasIndex(x => x.UserId).IsUnique();
            c.HasMany(x => x.Items)
             .WithOne(i => i.Cart)
             .HasForeignKey(i => i.CartId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CartItem>(ci =>
        {
            ci.HasKey(x => x.Id);
            ci.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
            ci.HasOne(x => x.Product)
              .WithMany(p => p.CartItems)
              .HasForeignKey(x => x.ProductId)
              .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(o =>
        {
            o.HasKey(x => x.Id);
            o.HasIndex(x => x.UserId);
            o.HasIndex(x => x.Status);
            o.Property(x => x.TotalAmount).HasPrecision(12, 2);
            o.Property(x => x.ShippingCharge).HasPrecision(12, 2);
            o.Property(x => x.GrandTotal).HasPrecision(12, 2);
            o.Property(x => x.FullName).HasMaxLength(150);
            o.Property(x => x.Address).HasMaxLength(500);
            o.Property(x => x.PhoneNumber).HasMaxLength(20);

            o.HasMany(x => x.Items)
             .WithOne(i => i.Order)
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(oi =>
        {
            oi.HasKey(x => x.Id);
            oi.Property(x => x.Price).HasPrecision(12, 2);
            oi.Property(x => x.LineTotal).HasPrecision(12, 2);
            oi.Property(x => x.ProductName).HasMaxLength(255);

            oi.HasOne(x => x.Product)
              .WithMany(p => p.OrderItems)
              .HasForeignKey(x => x.ProductId)
              .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FcmToken>(f =>
        {
            f.HasKey(x => x.Id);
            f.HasIndex(x => new { x.UserId, x.Token }).IsUnique();
        });

        builder.Entity<EcommerceNotification>(n =>
        {
            n.HasKey(x => x.Id);
            n.HasIndex(x => x.UserId);
            n.HasIndex(x => x.IsRead);
            n.Property(x => x.Title).HasMaxLength(255);
            n.Property(x => x.Message).HasMaxLength(1000);
        });
    }
}
