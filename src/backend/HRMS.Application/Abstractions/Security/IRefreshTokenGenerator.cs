namespace HRMS.Application.Abstractions.Security;

/// <summary>Creates high-entropy opaque refresh tokens and their persistence-safe hashes.</summary>
public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();
}
