using AuthService.DTOs;
using AuthService.Entities;
using AuthService.Services;
using Innovator.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPost("sso/login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 401)]
    public async Task<IActionResult> SsoLogin([FromBody] SsoLoginRequest request)
    {
        var result = await _authService.SsoLoginAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("sso/google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var result = await _authService.GoogleLoginAsync(request);
        // Return the flat payload the app reads (access_token / refresh_token / user).
        return result.Success ? Ok(result.Data) : Unauthorized(new { message = result.Message });
    }

    [HttpPost("token/refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.SendOtpAsync(request.Email, OtpPurpose.ForgotPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpRequest request)
    {
        var result = await _authService.VerifyOtpAsync(request with { Purpose = "email_verification" });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("forgot-password/verify")]
    public async Task<IActionResult> VerifyForgotOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await _authService.VerifyOtpAsync(request with { Purpose = "forgot_password" });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("forgot-password/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? User.FindFirstValue("sub")!);
        var result = await _authService.ChangePasswordAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] ResendOtpRequest request)
    {
        var purpose = request.Purpose == "forgot_password"
            ? OtpPurpose.ForgotPassword
            : OtpPurpose.EmailVerification;
        var result = await _authService.SendOtpAsync(request.Email, purpose);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        var result = await _authService.ResendOtpAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("resend-verification-otp")]
    public async Task<IActionResult> ResendVerificationOtp([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.SendOtpAsync(request.Email, OtpPurpose.EmailVerification);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
        return Ok(new { message = "Logged out." });
    }
}

[ApiController]
[Route("api")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("users/check-username")]
    public async Task<IActionResult> CheckUsername([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { message = "Username is required." });

        var result = await _authService.CheckUsernameAsync(username);
        return Ok(result);
    }
}
