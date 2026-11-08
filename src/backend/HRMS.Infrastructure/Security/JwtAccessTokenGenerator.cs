using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HRMS.Application.Abstractions.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRMS.Infrastructure.Security;

/// <summary>Issues HMAC-SHA256 signed JWT access tokens.</summary>
public sealed class JwtAccessTokenGenerator(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IAccessTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public GeneratedAccessToken Generate(UserLoginRecord user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_options.Issuer) ||
            string.IsNullOrWhiteSpace(_options.Audience))
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:Issuer and Authentication:Jwt:Audience must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey) ||
            Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey must contain at least 32 UTF-8 bytes. Configure it using .NET User Secrets or a secret store.");
        }

        if (_options.AccessTokenMinutes is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:AccessTokenMinutes must be between 1 and 30.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("tenant_code", user.TenantCode),
            new Claim("membership_id", user.MembershipId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new GeneratedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
