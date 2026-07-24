using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface ICartService
{
    Task<ApiResponse<List<CartItemDto>>> GetCartAsync(Guid userId);
    Task<ApiResponse<CartItemDto>> AddItemAsync(Guid userId, string productId);
    Task<ApiResponse<CartItemDto>> UpdateItemAsync(Guid userId, Guid cartItemId, int quantity);
    Task<ApiResponse<bool>> DeleteItemAsync(Guid userId, Guid cartItemId);
}

public class CartService : ICartService
{
    private readonly EcommerceDbContext _db;

    public CartService(EcommerceDbContext db) => _db = db;

    public async Task<ApiResponse<List<CartItemDto>>> GetCartAsync(Guid userId)
    {
        var cart = await GetOrCreateCartAsync(userId);

        var items = await _db.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync();

        return ApiResponse<List<CartItemDto>>.Ok(
            items.Select(ci => MapToDto(ci, cart.Id)).ToList());
    }

    public async Task<ApiResponse<CartItemDto>> AddItemAsync(Guid userId, string productId)
    {
        if (!Guid.TryParse(productId, out var prodId))
            return ApiResponse<CartItemDto>.Fail("Invalid product id.");

        var product = await _db.Products.FindAsync(prodId);
        if (product == null || !product.IsActive)
            return ApiResponse<CartItemDto>.Fail("Product not found.");

        if (product.Stock < 1)
            return ApiResponse<CartItemDto>.Fail("Product is out of stock.");

        var cart = await GetOrCreateCartAsync(userId);

        var existing = await _db.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == prodId);

        if (existing != null)
        {
            existing.Quantity++;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ApiResponse<CartItemDto>.Ok(MapToDto(existing, cart.Id));
        }

        var item = new CartItem
        {
            CartId = cart.Id,
            ProductId = prodId,
            Quantity = 1
        };

        _db.CartItems.Add(item);
        await _db.SaveChangesAsync();

        item.Product = product;
        return ApiResponse<CartItemDto>.Ok(MapToDto(item, cart.Id));
    }

    public async Task<ApiResponse<CartItemDto>> UpdateItemAsync(
        Guid userId, Guid cartItemId, int quantity)
    {
        var cart = await GetOrCreateCartAsync(userId);

        var item = await _db.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.CartId == cart.Id);

        if (item == null)
            return ApiResponse<CartItemDto>.Fail("Cart item not found.");

        if (quantity < 1)
            return ApiResponse<CartItemDto>.Fail("Quantity must be at least 1.");

        if (quantity > item.Product.Stock)
            return ApiResponse<CartItemDto>.Fail("Not enough stock.");

        item.Quantity = quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<CartItemDto>.Ok(MapToDto(item, cart.Id));
    }

    public async Task<ApiResponse<bool>> DeleteItemAsync(Guid userId, Guid cartItemId)
    {
        var cart = await GetOrCreateCartAsync(userId);

        var item = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.CartId == cart.Id);

        if (item == null)
            return ApiResponse<bool>.Fail("Cart item not found.");

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private async Task<Cart> GetOrCreateCartAsync(Guid userId)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart != null) return cart;

        cart = new Cart { UserId = userId };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();
        return cart;
    }

    private static CartItemDto MapToDto(CartItem ci, Guid cartId)
    {
        var total = ci.Product.Price * ci.Quantity;
        return new CartItemDto(
            ci.Id.ToString(),
            cartId.ToString(),
            ci.ProductId.ToString(),
            ci.Product.Name,
            (double)ci.Product.Price,
            ci.Quantity,
            (double)total);
    }
}
