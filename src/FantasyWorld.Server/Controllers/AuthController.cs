// FantasyWorld.Server/Controllers/AuthController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FantasyWorld.Server.Services;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService     authService,
    ILocalizationService loc) : ControllerBase
{
    private string Lang => Request.Headers["Accept-Language"]
        .FirstOrDefault()?.Split(',')[0].Split('-')[0] ?? "vi";

    private ClientType ClientTypeFromHeader()
    {
        var ua = Request.Headers.UserAgent.ToString();
        if (ua.Contains("J2ME") || ua.Contains("MIDP")) return ClientType.J2me;
        if (ua.Contains("UnityPlayer"))                  return ClientType.Unity;
        return Request.Headers["X-Client-Type"].FirstOrDefault() switch
        {
            "j2me"  => ClientType.J2me,
            "web"   => ClientType.Web,
            _       => ClientType.Unity,
        };
    }

    // ─── POST /api/auth/register ─────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiError(loc.Get("error.bad_request", Lang)));

        var (ok, msg, _) = await authService.RegisterAsync(
            req.Username, req.Password, req.Email, Lang);

        return ok
            ? StatusCode(201, new ApiOk(msg))
            : Conflict(new ApiError(msg));
    }

    // ─── POST /api/auth/login ────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiError(loc.Get("error.bad_request", Lang)));

        var ip         = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceInfo = Request.Headers["X-Device-Info"].FirstOrDefault();
        var clientType = ClientTypeFromHeader();

        var (ok, msg, data) = await authService.LoginAsync(
            req.Username, req.Password, Lang, clientType, ip, deviceInfo);

        return ok
            ? Ok(new { success = true, message = msg, data })
            : Unauthorized(new ApiError(msg));
    }

    // ─── POST /api/auth/logout ───────────────────────────────
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        await authService.LogoutAsync(token);
        return Ok(new ApiOk(loc.Get("auth.logout_success", Lang)));
    }

    // ─── POST /api/auth/refresh ──────────────────────────────
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var newToken = await authService.RefreshTokenAsync(req.RefreshToken, ClientTypeFromHeader());
        return newToken is null
            ? Unauthorized(new ApiError(loc.Get("auth.token_expired", Lang)))
            : Ok(new { success = true, accessToken = newToken });
    }

    // ─── GET /api/auth/me ────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var accountId = User.FindFirst("accountId")?.Value;
        var role      = User.FindFirst(ClaimTypes.Role)?.Value;
        return Ok(new { accountId, role });
    }
}

// ─── Request / Response models ───────────────────────────────

public record RegisterRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 4)]
    [property: System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-zA-Z0-9_]+$")]
    string Username,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 6)]
    string Password,

    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    string? Email = null,

    string? Phone = null
);

public record LoginRequest(
    [property: System.ComponentModel.DataAnnotations.Required] string Username,
    [property: System.ComponentModel.DataAnnotations.Required] string Password
);

public record RefreshRequest(
    [property: System.ComponentModel.DataAnnotations.Required] string RefreshToken
);

public record ApiOk(string Message)   { public bool Success => true; }
public record ApiError(string Message){ public bool Success => false; }
