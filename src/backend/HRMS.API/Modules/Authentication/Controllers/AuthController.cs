using System.IdentityModel.Tokens.Jwt;
using HRMS.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Modules.Authentication.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IRefreshTokenService refreshTokenService,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "hrms_refresh";
    private const string AuthCookiePath = "/api/v1/auth";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasTrustedOrigin())
        {
            return Forbid();
        }

        var session = await authenticationService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (session is null)
        {
            return Unauthorized();
        }

        SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(session.Login);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!HasTrustedOrigin())
        {
            return Forbid();
        }

        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        var result = await refreshTokenService.RefreshAsync(
            refreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (result.Status != RefreshTokenRotationStatus.Rotated || result.Session is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        var session = result.Session;
        SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);

        return Ok(new LoginResult(
            session.AccessToken.Value,
            "Bearer",
            session.AccessToken.ExpiresAtUtc,
            session.User.UserId,
            session.User.DisplayName,
            session.User.TenantId,
            session.User.TenantCode,
            session.User.TenantName));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!HasTrustedOrigin())
        {
            return Forbid();
        }

        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await refreshTokenService.RevokeAsync(
                refreshToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                "Logout",
                cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentIdentity()
    {
        return Ok(new
        {
            userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            tenantId = User.FindFirst("tenant_id")?.Value,
            tenantCode = User.FindFirst("tenant_code")?.Value,
            membershipId = User.FindFirst("membership_id")?.Value
        });
    }

    private bool HasTrustedOrigin()
    {
        var origin = Request.Headers["Origin"].ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        var configuredOrigins = configuration
            .GetSection("Authentication:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (configuredOrigins.Length > 0)
        {
            return configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
               string.Equals(originUri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(originUri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    private void SetRefreshCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            refreshToken,
            BuildRefreshCookieOptions(expiresAtUtc));
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(
            RefreshCookieName,
            BuildRefreshCookieOptions(DateTime.UtcNow.AddDays(-1)));

    private CookieOptions BuildRefreshCookieOptions(DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = AuthCookiePath,
        Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
        IsEssential = true
    };
}
