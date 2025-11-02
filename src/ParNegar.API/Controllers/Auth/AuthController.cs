using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Shared.DTOs.Auth;
using ParNegar.Shared.DTOs.Common;

namespace ParNegar.API.Controllers.Auth;

/// <summary>
/// Authentication controller
/// Route: /api/Auth/Auth
/// </summary>
[ApiController]
[Route("api/Auth/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Login - دریافت توکن با username و password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

        var response = await _authService.LoginAsync(request, ipAddress, userAgent, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Refresh Token - دریافت توکن جدید با RefreshToken
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

        var response = await _tokenService.RefreshTokenAsync(
            request.AccessToken,
            request.RefreshToken,
            ipAddress,
            userAgent,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Logout - خروج از سشن جاری
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = User.FindFirst("session_id")?.Value;

        if (string.IsNullOrEmpty(sessionId))
        {
            return BadRequest(new { message = "Session ID not found" });
        }

        await _authService.LogoutAsync(sessionId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Get Active Sessions - دریافت لیست سشن‌های فعال کاربر
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSessions(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        var currentSessionId = User.FindFirst("session_id")?.Value;

        var sessions = await _authService.GetActiveSessionsAsync(userId, currentSessionId, cancellationToken);

        return Ok(sessions);
    }

    /// <summary>
    /// Get Current Session - دریافت اطلاعات سشن جاری
    /// </summary>
    [HttpGet("sessions/current")]
    [Authorize]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentSession(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var currentSessionId = User.FindFirst("session_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(currentSessionId))
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        var sessions = await _authService.GetActiveSessionsAsync(userId, currentSessionId, cancellationToken);

        var currentSession = sessions.FirstOrDefault(s => s.IsCurrentSession);

        if (currentSession == null)
        {
            return NotFound(new { message = "Current session not found" });
        }

        return Ok(currentSession);
    }

    /// <summary>
    /// Get Active Sessions Count - تعداد سشن‌های فعال
    /// </summary>
    [HttpGet("sessions/count")]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSessionsCount(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        var count = await _authService.GetActiveSessionsCountAsync(userId, cancellationToken);

        return Ok(new { count });
    }

    /// <summary>
    /// Revoke Session - بستن یک سشن خاص
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(string sessionId, CancellationToken cancellationToken)
    {
        await _authService.RevokeSessionAsync(sessionId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Revoke All Sessions - بستن همه سشن‌ها (شامل سشن جاری)
    /// </summary>
    [HttpDelete("sessions/all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        await _authService.RevokeAllSessionsAsync(userId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Revoke Other Sessions - بستن همه سشن‌ها به جز سشن جاری
    /// </summary>
    [HttpDelete("sessions/others")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var currentSessionId = User.FindFirst("session_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(currentSessionId))
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdClaim);
        await _authService.RevokeAllSessionsExceptCurrentAsync(userId, currentSessionId, cancellationToken);

        return NoContent();
    }
}
