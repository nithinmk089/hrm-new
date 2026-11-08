using System.Security.Cryptography;
using System.Text;
using HRMS.Application.Abstractions.Security;
using HRMS.Application.Security;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task CreateSessionAsync_PersistsHash_AndReturnsAccessAndRefreshTokens()
    {
        var user = CreateUser();
        var repository = new FakeRefreshTokenRepository();
        var generator = new FakeRefreshTokenGenerator("new-refresh-token");
        var accessTokens = new FakeAccessTokenGenerator();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));
        var sut = CreateService(repository, generator, accessTokens, clock);

        var result = await sut.CreateSessionAsync(user, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal("access-token", result.AccessToken.Value);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddDays(14), result.RefreshTokenExpiresAtUtc);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("new-refresh-token")), repository.CreatedHash);
        Assert.Equal(user.UserId, repository.CreatedUser?.UserId);
        Assert.Equal("127.0.0.1", repository.CreatedByIp);
        Assert.Equal(1, accessTokens.GenerateCalls);
    }

    [Fact]
    public async Task RefreshAsync_RotatesToken_AndIssuesNewAccessToken()
    {
        var user = CreateUser();
        var repository = new FakeRefreshTokenRepository
        {
            RotationResult = new RefreshTokenRotationResult(RefreshTokenRotationStatus.Rotated, user)
        };
        var generator = new FakeRefreshTokenGenerator("replacement-refresh-token");
        var accessTokens = new FakeAccessTokenGenerator();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));
        var sut = CreateService(repository, generator, accessTokens, clock);

        var result = await sut.RefreshAsync("current-refresh-token", "127.0.0.1");

        Assert.Equal(RefreshTokenRotationStatus.Rotated, result.Status);
        Assert.NotNull(result.Session);
        Assert.Equal("replacement-refresh-token", result.Session.RefreshToken);
        Assert.Equal(user.UserId, result.Session.User.UserId);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("current-refresh-token")), repository.CurrentHash);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("replacement-refresh-token")), repository.ReplacementHash);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddDays(14), repository.ReplacementExpiresAtUtc);
        Assert.Equal(1, accessTokens.GenerateCalls);
    }

    [Theory]
    [InlineData(RefreshTokenRotationStatus.Invalid)]
    [InlineData(RefreshTokenRotationStatus.ReuseDetected)]
    public async Task RefreshAsync_DoesNotIssueSession_WhenRotationFails(RefreshTokenRotationStatus status)
    {
        var repository = new FakeRefreshTokenRepository
        {
            RotationResult = new RefreshTokenRotationResult(status, null)
        };
        var accessTokens = new FakeAccessTokenGenerator();
        var sut = CreateService(
            repository,
            new FakeRefreshTokenGenerator("replacement"),
            accessTokens,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await sut.RefreshAsync("current-refresh-token", null);

        Assert.Equal(status, result.Status);
        Assert.Null(result.Session);
        Assert.Equal(0, accessTokens.GenerateCalls);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsInvalid_WithoutCallingRepository_ForBlankToken()
    {
        var repository = new FakeRefreshTokenRepository();
        var sut = CreateService(
            repository,
            new FakeRefreshTokenGenerator("replacement"),
            new FakeAccessTokenGenerator(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await sut.RefreshAsync("  ", null);

        Assert.Equal(RefreshTokenRotationStatus.Invalid, result.Status);
        Assert.Null(result.Session);
        Assert.Null(repository.CurrentHash);
    }

    [Fact]
    public async Task RevokeAsync_HashesRawTokenBeforePersistence()
    {
        var repository = new FakeRefreshTokenRepository { RevokeResult = true };
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));
        var sut = CreateService(
            repository,
            new FakeRefreshTokenGenerator("unused"),
            new FakeAccessTokenGenerator(),
            clock);

        var result = await sut.RevokeAsync("logout-refresh-token", "127.0.0.1");

        Assert.True(result);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("logout-refresh-token")), repository.RevokedHash);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, repository.RevokedAtUtc);
        Assert.Equal("Logout", repository.RevocationReason);
    }

    private static RefreshTokenService CreateService(
        FakeRefreshTokenRepository repository,
        FakeRefreshTokenGenerator generator,
        FakeAccessTokenGenerator accessTokens,
        TimeProvider clock) =>
        new(generator, repository, accessTokens, clock, new RefreshTokenSettings());

    private static UserLoginRecord CreateUser() => new(
        Guid.NewGuid(), "Person", "person@example.com", "hash", true, 0, null,
        Guid.NewGuid(), "ACME", "Acme Tenant", Guid.NewGuid());

    private sealed class FakeRefreshTokenGenerator(string value) : IRefreshTokenGenerator
    {
        public GeneratedRefreshToken Generate() =>
            new(value, SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
    {
        public int GenerateCalls { get; private set; }

        public GeneratedAccessToken Generate(UserLoginRecord user)
        {
            GenerateCalls++;
            return new GeneratedAccessToken("access-token", DateTime.UtcNow.AddMinutes(10));
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public UserLoginRecord? CreatedUser { get; private set; }
        public byte[]? CreatedHash { get; private set; }
        public string? CreatedByIp { get; private set; }
        public byte[]? CurrentHash { get; private set; }
        public byte[]? ReplacementHash { get; private set; }
        public DateTime ReplacementExpiresAtUtc { get; private set; }
        public byte[]? RevokedHash { get; private set; }
        public DateTime RevokedAtUtc { get; private set; }
        public string? RevocationReason { get; private set; }
        public bool RevokeResult { get; init; }
        public RefreshTokenRotationResult RotationResult { get; init; } =
            new(RefreshTokenRotationStatus.Invalid, null);

        public Task<bool> CreateAsync(
            UserLoginRecord user, Guid refreshTokenId, byte[] tokenHash,
            DateTime expiresAtUtc, string? createdByIp, CancellationToken cancellationToken = default)
        {
            CreatedUser = user;
            CreatedHash = tokenHash;
            CreatedByIp = createdByIp;
            return Task.FromResult(true);
        }

        public Task<RefreshTokenRotationResult> RotateAsync(
            byte[] currentTokenHash, Guid replacementTokenId, byte[] replacementTokenHash,
            DateTime nowUtc, DateTime replacementExpiresAtUtc, string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            CurrentHash = currentTokenHash;
            ReplacementHash = replacementTokenHash;
            ReplacementExpiresAtUtc = replacementExpiresAtUtc;
            return Task.FromResult(RotationResult);
        }

        public Task<bool> RevokeAsync(
            byte[] tokenHash, DateTime revokedAtUtc, string? ipAddress, string reason,
            CancellationToken cancellationToken = default)
        {
            RevokedHash = tokenHash;
            RevokedAtUtc = revokedAtUtc;
            RevocationReason = reason;
            return Task.FromResult(RevokeResult);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
