using System.Security.Cryptography;
using System.Text;
using HRMS.Application.Abstractions.Security;
using HRMS.Infrastructure.Security;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class RefreshTokenGeneratorTests
{
    private readonly IRefreshTokenGenerator _sut = new RefreshTokenGenerator();

    [Fact]
    public void Generate_ReturnsUrlSafeTokenWith256BitsOfEntropy()
    {
        var token = _sut.Generate();

        Assert.Equal(43, token.Value.Length);
        Assert.All(token.Value, character =>
            Assert.True(
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_',
                "Refresh token must be Base64URL encoded."));
        Assert.Equal(32, Convert.FromBase64String(
            token.Value.Replace('-', '+').Replace('_', '/') + "=").Length);
    }

    [Fact]
    public void Generate_ReturnsSha256HashOfRawToken()
    {
        var token = _sut.Generate();

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token.Value));

        Assert.Equal(32, token.Hash.Length);
        Assert.Equal(expectedHash, token.Hash);
    }

    [Fact]
    public void Generate_ProducesUniqueTokensAndHashes()
    {
        var first = _sut.Generate();
        var second = _sut.Generate();

        Assert.NotEqual(first.Value, second.Value);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
