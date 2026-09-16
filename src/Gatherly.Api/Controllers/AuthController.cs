using System.Security.Claims;
using Gatherly.Contracts.Authentication;
using Gatherly.Contracts.Common;
using Gatherly.Services.Interfaces.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherly.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _authService.RegisterAsync(request, ipAddress, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponse>.Ok(result, "Registration successful."));
        }
        catch (ArgumentException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Login with email and password</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _authService.LoginAsync(request, ipAddress, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Refresh the access token using a refresh token</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = GetIpAddress();
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Logout and revoke the refresh token</summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = GetIpAddress();
            await _authService.LogoutAsync(request.RefreshToken, ipAddress, cancellationToken);
            return Ok(ApiResponse.Ok("Logged out successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Request a password reset email</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ForgotPasswordAsync(request, cancellationToken);
            return Ok(ApiResponse.Ok("If an account with that email exists, a reset link has been sent."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forgot-password");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Reset password using a reset token</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(ApiResponse.Ok("Password reset successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reset-password");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Change password for the authenticated user</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized(ApiResponse.Fail("Unauthorized."));

            await _authService.ChangePasswordAsync(userId.Value, request, cancellationToken);
            return Ok(ApiResponse.Ok("Password changed successfully."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change-password");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    /// <summary>Get the current user's profile</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized(ApiResponse.Fail("Unauthorized."));

            var profile = await _authService.GetProfileAsync(userId.Value, cancellationToken);
            return Ok(ApiResponse<UserDto>.Ok(profile));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile");
            return StatusCode(500, ApiResponse.Fail("An unexpected error occurred."));
        }
    }

    private string GetIpAddress()
        // ForwardedHeadersMiddleware (configured in Program.cs) already updates
        // RemoteIpAddress from the X-Forwarded-For header when the request originates
        // from a trusted proxy. Reading the header directly here is unsafe because any
        // client can forge it — always use the already-resolved RemoteIpAddress instead.
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private long? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(idClaim, out var id) ? id : null;
    }
}
