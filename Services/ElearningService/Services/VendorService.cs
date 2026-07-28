using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Innovator.Shared.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface IVendorService
{
    Task<ApiResponse<VendorAccountDto>> CreateAsync(CreateVendorRequest request);
    Task<ApiResponse<List<VendorAccountDto>>> ListAsync();
    Task<ApiResponse<VendorAccountDto>> UpdateAsync(Guid id, UpdateVendorRequest request);
    Task<ApiResponse<bool>> DeleteAsync(Guid id);
    Task<ApiResponse<VendorLoginResponse>> LoginAsync(VendorLoginRequest request);
}

public class VendorService : IVendorService
{
    private readonly ElearningDbContext _db;
    private readonly IConfiguration _config;
    private static readonly PasswordHasher<Vendor> Hasher = new();

    public VendorService(ElearningDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ApiResponse<VendorAccountDto>> CreateAsync(CreateVendorRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        if (await _db.Vendors.AnyAsync(v => v.Username == username))
            return ApiResponse<VendorAccountDto>.Fail("A vendor with this username already exists.");

        var vendor = new Vendor
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Username = username,
            IsActive = true
        };
        vendor.PasswordHash = Hasher.HashPassword(vendor, request.Password);

        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();

        return ApiResponse<VendorAccountDto>.Ok(Map(vendor), "Vendor created.");
    }

    public async Task<ApiResponse<List<VendorAccountDto>>> ListAsync()
    {
        var vendors = await _db.Vendors
            .OrderByDescending(v => v.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return ApiResponse<List<VendorAccountDto>>.Ok(vendors.Select(Map).ToList());
    }

    public async Task<ApiResponse<VendorAccountDto>> UpdateAsync(Guid id, UpdateVendorRequest request)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null) return ApiResponse<VendorAccountDto>.Fail("Vendor not found.");

        if (!string.IsNullOrWhiteSpace(request.Name)) vendor.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email)) vendor.Email = request.Email.Trim();
        if (request.IsActive.HasValue) vendor.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.Password))
            vendor.PasswordHash = Hasher.HashPassword(vendor, request.Password);

        vendor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<VendorAccountDto>.Ok(Map(vendor), "Vendor updated.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (vendor is null) return ApiResponse<bool>.Fail("Vendor not found.");

        _db.Vendors.Remove(vendor);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Vendor deleted.");
    }

    public async Task<ApiResponse<VendorLoginResponse>> LoginAsync(VendorLoginRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Username == username);

        if (vendor is null)
            return ApiResponse<VendorLoginResponse>.Fail("Invalid username or password.");

        if (!vendor.IsActive)
            return ApiResponse<VendorLoginResponse>.Fail("This vendor account is disabled.");

        var verify = Hasher.VerifyHashedPassword(vendor, vendor.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
            return ApiResponse<VendorLoginResponse>.Fail("Invalid username or password.");

        var secret = _config["Jwt:Secret"]!;
        // Role "vendor" so VendorScope treats them as a non-admin owner; the token
        // sub == vendor.Id, which matches Course.Vendor for ownership checks.
        var token = JwtHelper.GenerateToken(
            vendor.Id, vendor.Username, vendor.Email, "vendor", secret, expiryMinutes: 720);
        var refresh = JwtHelper.GenerateRefreshToken();

        return ApiResponse<VendorLoginResponse>.Ok(
            new VendorLoginResponse(token, refresh, Map(vendor)), "Login successful.");
    }

    private static VendorAccountDto Map(Vendor v) => new(
        v.Id.ToString(),
        v.Name,
        v.Email,
        v.Username,
        v.IsActive,
        v.CreatedAt.ToString("o"));
}
