using HRMS.Application.Abstractions.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HRMS.Infrastructure.Security;

/// <summary>Uses the versioned ASP.NET Core Identity password-hashing format (PBKDF2).</summary>
public sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private static readonly object PasswordSubject = new();

    private readonly PasswordHasher<object> _passwordHasher;

    public AspNetCorePasswordHasher(IOptions<PasswordHasherOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _passwordHasher = new PasswordHasher<object>(options);
    }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _passwordHasher.HashPassword(PasswordSubject, password);
    }

    public PasswordVerificationOutcome VerifyHashedPassword(
        string passwordHash,
        string providedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(providedPassword);

        var result = _passwordHasher.VerifyHashedPassword(
            PasswordSubject,
            passwordHash,
            providedPassword);

        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Succeeded,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed
        };
    }
}
