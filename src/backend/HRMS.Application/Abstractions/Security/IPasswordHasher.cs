namespace HRMS.Application.Abstractions.Security;

/// <summary>Hashes passwords and verifies submitted credentials without exposing a hashing algorithm to Application.</summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    PasswordVerificationOutcome VerifyHashedPassword(string passwordHash, string providedPassword);
}
