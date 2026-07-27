using AuthService.Data;
using AuthService.DTOs;
using AuthService.Entities;
using Google.Apis.Auth;
using Innovator.Shared.DTOs;
using Innovator.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<AuthResponse>> SsoLoginAsync(SsoLoginRequest request);
    Task<ApiResponse<GoogleLoginResponse>> GoogleLoginAsync(GoogleLoginRequest request);
    Task<ApiResponse<AuthResponse>> RefreshTokenAsync(string refreshToken);
    Task<ApiResponse<bool>> SendOtpAsync(string email, OtpPurpose purpose);
    Task<ApiResponse<bool>> VerifyOtpAsync(VerifyOtpRequest request);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ApiResponse<CheckUsernameResponse>> CheckUsernameAsync(string username);
    Task<ApiResponse<bool>> ResendOtpAsync(ResendOtpRequest request);
    Task RevokeRefreshTokenAsync(string token);
}

public class AuthBusinessService : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthBusinessService> _logger;

    private string JwtSecret => _config["Jwt:Secret"]!;
    private int AccessTokenMinutes => int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "60");
    private int RefreshTokenDays => int.Parse(_config["Jwt:RefreshTokenDays"] ?? "30");

    public AuthBusinessService(
        AuthDbContext db,
        IEmailService emailService,
        IConfiguration config,
        ILogger<AuthBusinessService> logger)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLower());
        if (emailExists)
            return ApiResponse<AuthResponse>.Fail("Email is already registered.");

        var usernameExists = await _db.Users.AnyAsync(u => u.Username == request.Username.ToLower());
        if (usernameExists)
            return ApiResponse<AuthResponse>.Fail("Username is already taken.");

        var user = new User
        {
            Username = request.Username.ToLower().Trim(),
            Email = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            Phone = request.Phone,
            IsEmailVerified = false
        };

        _db.Users.Add(user);

        var otp = GenerateOtp();
        _db.OtpRecords.Add(new OtpRecord
        {
            Code = otp,
            Purpose = OtpPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UserId = user.Id
        });

        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendOtpAsync(user.Email, otp, "email_verification");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OTP email failed for {Email} — registration still succeeded", user.Email);
        }

        var (access, refresh) = await IssueTokensAsync(user);

        return ApiResponse<AuthResponse>.Ok(
            BuildAuthResponse(user, access, refresh),
            "Registration successful. Please verify your email.");
    }

    public async Task<ApiResponse<AuthResponse>> SsoLoginAsync(SsoLoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email.ToLower() ||
                u.Username == request.Email.ToLower());

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return ApiResponse<AuthResponse>.Fail("Invalid credentials.");

        if (!user.IsActive)
            return ApiResponse<AuthResponse>.Fail("Account is suspended.");

        // Migrated users still carry Django's PBKDF2 hash — upgrade to BCrypt now.
        if (PasswordHasher.NeedsUpgrade(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
            user.UpdatedAt = DateTime.UtcNow;
        }

        var (access, refresh) = await IssueTokensAsync(user);

        return ApiResponse<AuthResponse>.Ok(BuildAuthResponse(user, access, refresh), "Login successful.");
    }

    public async Task<ApiResponse<GoogleLoginResponse>> GoogleLoginAsync(GoogleLoginRequest request)
    {
        // Verify the Google ID token: signature, issuer, expiry, and audience
        // (must be one of our OAuth client IDs from appsettings Google:ClientIds).
        var clientIds = _config.GetSection("Google:ClientIds").Get<string[]>() ?? Array.Empty<string>();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (clientIds.Length > 0)
                settings.Audience = clientIds;

            payload = await GoogleJsonWebSignature.ValidateAsync(request.GoogleToken, settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            return ApiResponse<GoogleLoginResponse>.Fail("Invalid Google token.");
        }

        var email = (payload.Email ?? string.Empty).ToLower().Trim();
        if (string.IsNullOrEmpty(email))
            return ApiResponse<GoogleLoginResponse>.Fail("Google account has no email.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new User
            {
                Username = await GenerateUniqueUsernameAsync(email, payload.Name),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Role = "innovator",
                IsEmailVerified = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true; // Google has verified the address
        }

        if (!user.IsActive)
            return ApiResponse<GoogleLoginResponse>.Fail("Account is suspended.");

        var (access, refresh) = await IssueTokensAsync(user);

        return ApiResponse<GoogleLoginResponse>.Ok(new GoogleLoginResponse(
            access,
            refresh,
            new GoogleUserDto(user.Id.ToString(), user.Username, user.Email, user.Role, user.IsEmailVerified)),
            "Login successful.");
    }

    private async Task<string> GenerateUniqueUsernameAsync(string email, string? displayName)
    {
        var seed = email.Split('@')[0];
        if (string.IsNullOrWhiteSpace(seed) && !string.IsNullOrWhiteSpace(displayName))
            seed = displayName;

        var baseName = new string(seed.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.')
            .ToArray());

        if (baseName.Length < 3)
            baseName = $"user{baseName}";
        if (baseName.Length > 40)
            baseName = baseName[..40];

        var candidate = baseName;
        var suffix = 0;
        while (await _db.Users.AnyAsync(u => u.Username == candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked);

        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<AuthResponse>.Fail("Invalid or expired refresh token.");

        stored.IsRevoked = true;

        var (access, newRefresh) = await IssueTokensAsync(stored.User);

        return ApiResponse<AuthResponse>.Ok(
            BuildAuthResponse(stored.User, access, newRefresh));
    }

    public async Task<ApiResponse<bool>> SendOtpAsync(string email, OtpPurpose purpose)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());
        if (user == null)
            return ApiResponse<bool>.Fail("No account found with that email.");

        var otp = GenerateOtp();

        _db.OtpRecords.Add(new OtpRecord
        {
            Code = otp,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UserId = user.Id
        });

        await _db.SaveChangesAsync();

        var purposeStr = purpose == OtpPurpose.ForgotPassword ? "forgot_password" : "email_verification";
        await _emailService.SendOtpAsync(email, otp, purposeStr);

        return ApiResponse<bool>.Ok(true, "OTP sent to your email.");
    }

    public async Task<ApiResponse<bool>> VerifyOtpAsync(VerifyOtpRequest request)
    {
        var purpose = request.Purpose == "forgot_password"
            ? OtpPurpose.ForgotPassword
            : OtpPurpose.EmailVerification;

        var user = await _db.Users
            .Include(u => u.OtpRecords)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

        if (user == null)
            return ApiResponse<bool>.Fail("Account not found.");

        var record = user.OtpRecords
            .Where(o => o.Purpose == purpose && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (record == null || record.Code != request.Code)
            return ApiResponse<bool>.Fail("Invalid or expired OTP.");

        record.IsUsed = true;

        if (purpose == OtpPurpose.EmailVerification)
            user.IsEmailVerified = true;

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "OTP verified.");
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var verifyResult = await VerifyOtpAsync(new VerifyOtpRequest(
            request.Email, request.Code, "forgot_password"));

        if (!verifyResult.Success)
            return ApiResponse<bool>.Fail(verifyResult.Message);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());
        if (user == null) return ApiResponse<bool>.Fail("Account not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        var tokens = await _db.RefreshTokens.Where(r => r.UserId == user.Id).ToListAsync();
        tokens.ForEach(t => t.IsRevoked = true);

        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Password reset successful.");
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return ApiResponse<bool>.Fail("User not found.");

        if (!PasswordHasher.Verify(request.OldPassword, user.PasswordHash))
            return ApiResponse<bool>.Fail("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Password changed.");
    }

    public async Task<ApiResponse<CheckUsernameResponse>> CheckUsernameAsync(string username)
    {
        var clean = username.ToLower().Trim();
        var taken = await _db.Users.AnyAsync(u => u.Username == clean);

        if (!taken)
            return ApiResponse<CheckUsernameResponse>.Ok(
                new CheckUsernameResponse(true, new List<string>()));

        var suggestions = Enumerable.Range(1, 5)
            .Select(i => $"{clean}{Random.Shared.Next(100, 9999)}")
            .ToList();

        return ApiResponse<CheckUsernameResponse>.Ok(
            new CheckUsernameResponse(false, suggestions),
            "Username taken.");
    }

    public async Task<ApiResponse<bool>> ResendOtpAsync(ResendOtpRequest request)
    {
        var purpose = request.Purpose == "forgot_password"
            ? OtpPurpose.ForgotPassword
            : OtpPurpose.EmailVerification;

        return await SendOtpAsync(request.Email, purpose);
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
        if (stored != null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<(string access, string refresh)> IssueTokensAsync(User user)
    {
        var access = JwtHelper.GenerateToken(
            user.Id, user.Username, user.Email, user.Role,
            JwtSecret, AccessTokenMinutes);

        var refresh = JwtHelper.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
            UserId = user.Id
        });

        await _db.SaveChangesAsync();

        return (access, refresh);
    }

    private AuthResponse BuildAuthResponse(User user, string access, string refresh) =>
        new(
            AccessToken: access,
            RefreshToken: refresh,
            ExpiresIn: AccessTokenMinutes * 60,
            User: new UserAuthDto(user.Id, user.Username, user.Email, user.Role, user.IsEmailVerified));

    private static string GenerateOtp() =>
        Random.Shared.Next(100000, 999999).ToString();
}
