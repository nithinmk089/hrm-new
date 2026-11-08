namespace HRMS.Infrastructure.Security;

/// <summary>JWT issuer, audience, signing secret and access-token lifetime.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 10;
}
