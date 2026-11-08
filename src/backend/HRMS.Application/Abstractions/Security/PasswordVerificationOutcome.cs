namespace HRMS.Application.Abstractions.Security;

/// <summary>Result of verifying a password against a stored adaptive hash.</summary>
public enum PasswordVerificationOutcome
{
    Failed = 0,
    Succeeded = 1,
    SuccessRehashNeeded = 2
}
