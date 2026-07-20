using Innovator.Shared.Entities;

namespace AuthService.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "innovator";
    public bool IsEmailVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? Phone { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = new();
    public List<OtpRecord> OtpRecords { get; set; } = new();
}

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public class OtpRecord : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public OtpPurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public enum OtpPurpose
{
    EmailVerification,
    ForgotPassword,
    PhoneVerification
}
