namespace HRMS.Application.Abstractions.Security;

/// <summary>Creates signed access tokens for an authenticated tenant membership.</summary>
public interface IAccessTokenGenerator
{
    GeneratedAccessToken Generate(UserLoginRecord user);
}
