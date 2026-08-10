using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuthService.DTOs;

/// <summary>Body sent by the Flutter app: {"google_token": "<Google ID token>"}.</summary>
public record GoogleLoginRequest(
    [property: JsonPropertyName("google_token")]
    [Required] string GoogleToken
);

/// <summary>
/// Flat response the app's Google-login parser reads:
/// access_token / refresh_token / user{...}. Deliberately NOT the usual envelope.
/// </summary>
public record GoogleLoginResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("user")] GoogleUserDto User
);

public record GoogleUserDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("is_email_verified")] bool IsEmailVerified
);

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

public record ChangeEmailRequest(
    [Required, EmailAddress] string NewEmail,
    [Required] string Password
);

public record VerifyEmailChangeRequest(
    [Required, StringLength(6, MinimumLength = 4)] string Code
);

public record DeleteAccountRequest(
    [Required] string Password
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
