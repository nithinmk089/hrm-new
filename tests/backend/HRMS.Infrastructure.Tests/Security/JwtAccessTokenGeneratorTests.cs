using System.IdentityModel.Tokens.Jwt;
using HRMS.Application.Abstractions.Security;
using HRMS.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class JwtAccessTokenGeneratorTests
{
    [Fact]
    public void Generate_ProducesSignedTokenWithUserAndTenantClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "HRMS",
            Audience = "HRMS.API",
            SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
            AccessTokenMinutes = 10
        });
        var user = new UserLoginRecord(Guid.NewGuid(), "Person", "person@example.com", "stored-hash", true, 0, null,
            Guid.NewGuid(), "ACME", "Acme Tenant", Guid.NewGuid());
        var sut = new JwtAccessTokenGenerator(options, TimeProvider.System);

        var result = sut.Generate(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value);

        Assert.Equal("HRMS", token.Issuer);
        Assert.Contains("HRMS.API", token.Audiences);
        Assert.Equal(user.UserId.ToString(), token.Subject);
        Assert.Equal(user.Email, token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.TenantId.ToString(), token.Claims.Single(claim => claim.Type == "tenant_id").Value);
        Assert.Equal(user.MembershipId.ToString(), token.Claims.Single(claim => claim.Type == "membership_id").Value);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void Generate_Throws_WhenSigningKeyIsTooShort()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "HRMS",
            Audience = "HRMS.API",
            SigningKey = "too-short",
            AccessTokenMinutes = 10
        });
        var user = new UserLoginRecord(Guid.NewGuid(), "Person", "person@example.com", "hash", true, 0, null,
            Guid.NewGuid(), "ACME", "Acme Tenant", Guid.NewGuid());
        var sut = new JwtAccessTokenGenerator(options, TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => sut.Generate(user));
    }
}
