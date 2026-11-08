using System.Security.Cryptography;
using System.Text;
using HRMS.Application.Abstractions.Security;

namespace HRMS.Infrastructure.Security;

/// <summary>
/// Generates 256-bit opaque refresh tokens. Only the SHA-256 digest is suitable for persistence.
/// </summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenSizeBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var tokenValue = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue));
        CryptographicOperations.ZeroMemory(randomBytes);

        return new GeneratedRefreshToken(tokenValue, hash);
    }
}
