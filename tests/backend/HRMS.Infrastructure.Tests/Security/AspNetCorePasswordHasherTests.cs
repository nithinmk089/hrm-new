using HRMS.Application.Abstractions.Security;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class AspNetCorePasswordHasherTests
{
    private readonly IPasswordHasher _sut = new AspNetCorePasswordHasher(
        Options.Create(new PasswordHasherOptions()));

    [Fact]
    public void HashPassword_ReturnsNonPlaintextVersionedHash()
    {
        const string password = "Correct-Horse-Battery-42!";

        var hash = _sut.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.StartsWith("AQAAAA", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyHashedPassword_ReturnsSucceeded_ForCorrectPassword()
    {
        const string password = "Correct-Horse-Battery-42!";
        var hash = _sut.HashPassword(password);

        var result = _sut.VerifyHashedPassword(hash, password);

        Assert.Equal(PasswordVerificationOutcome.Succeeded, result);
    }

    [Fact]
    public void VerifyHashedPassword_ReturnsFailed_ForIncorrectPassword()
    {
        var hash = _sut.HashPassword("Correct-Horse-Battery-42!");

        var result = _sut.VerifyHashedPassword(hash, "Wrong-Password-42!");

        Assert.Equal(PasswordVerificationOutcome.Failed, result);
    }

    [Fact]
    public void HashPassword_Throws_WhenPasswordIsBlank()
    {
        Assert.Throws<ArgumentException>(() => _sut.HashPassword(" "));
    }
}
