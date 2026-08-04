using Innovator.Shared.Entities;

namespace EcommerceService.Entities;

public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Product> Products { get; set; } = new();
}

// A promotional banner shown at the top of the shop. Admin uploads the image
// and links it to a product; tapping the banner in the app opens that product.
public class Banner : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? Image { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public List<ProductImage> Images { get; set; } = new();
    public List<CartItem> CartItems { get; set; } = new();
    public List<OrderItem> OrderItems { get; set; } = new();
}

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Image { get; set; } = string.Empty;
}

public class Cart : BaseEntity
{
    public Guid UserId { get; set; }
    public List<CartItem> Items { get; set; } = new();
}

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}

public class Order : BaseEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string PaymentType { get; set; } = "cod";
    public string Status { get; set; } = "pending";
    public decimal TotalAmount { get; set; }
    public decimal ShippingCharge { get; set; } = 0;
    public decimal GrandTotal { get; set; }
    public string? PaymentScreenshotPath { get; set; }
    public string? KhaltiPidx { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string? Image { get; set; }
}

public class PaymentQr : BaseEntity
{
    public string VendorId { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class FcmToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
}

public class EcommerceNotification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = "order";
    public bool IsRead { get; set; } = false;
    public string DataJson { get; set; } = "{}";
}
