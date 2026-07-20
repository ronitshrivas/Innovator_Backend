using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(50)] string Username,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? Phone,
    string Role = "innovator"
);

public record LoginRequest(
    [Required] string EmailOrUsername,
    [Required] string Password
);

public record SsoLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

public record VerifyOtpRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(6, MinimumLength = 4)] string Code,
    string Purpose = "forgot_password"
);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Code,
    [Required, MinLength(8)] string NewPassword
);

public record ChangePasswordRequest(
    [Required] string OldPassword,
    [Required, MinLength(8)] string NewPassword
);

public record RefreshTokenRequest(
    [Required] string RefreshToken
);

public record ResendOtpRequest(
    [Required, EmailAddress] string Email,
    string Purpose = "email_verification"
);

public record CheckUsernameRequest(
    [Required, MinLength(3)] string Username
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserAuthDto User
);

public record UserAuthDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsEmailVerified
);

public record CheckUsernameResponse(
    bool Available,
    List<string> Suggestions
);
