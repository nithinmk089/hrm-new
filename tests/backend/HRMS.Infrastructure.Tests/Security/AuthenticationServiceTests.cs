using HRMS.Application.Abstractions.Security;
using HRMS.Application.Security;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsNull_AndRecordsFailure_ForIncorrectPassword()
    {
        var user = CreateUser();
        var repository = new FakeIdentityRepository(user);
        var refresh = new FakeRefreshTokenService(user);
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.Failed), refresh);

        var result = await sut.LoginAsync(new LoginRequest(" person@example.com ", "wrong", " acme "), "127.0.0.1");

        Assert.Null(result);
        Assert.Equal("PERSON@EXAMPLE.COM", repository.LastNormalizedEmail);
        Assert.Equal("ACME", repository.LastNormalizedTenantCode);
        Assert.Equal(1, repository.FailedLoginCalls);
        Assert.Equal(0, repository.SuccessfulLoginCalls);
        Assert.Equal(0, refresh.CreateCalls);
    }

    [Fact]
    public async Task LoginAsync_ReturnsSafeLoginAndRefreshSession_ForValidCredentials()
    {
        var user = CreateUser();
        var repository = new FakeIdentityRepository(user);
        var refresh = new FakeRefreshTokenService(user);
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.Succeeded), refresh);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"), "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal("Bearer", result.Login.TokenType);
        Assert.Equal("test-token", result.Login.AccessToken);
        Assert.Equal(user.UserId, result.Login.UserId);
        Assert.Equal(user.TenantId, result.Login.TenantId);
        Assert.Equal("test-refresh-token", result.RefreshToken);
        Assert.Equal(1, repository.SuccessfulLoginCalls);
        Assert.Equal(1, refresh.CreateCalls);
        Assert.Equal("127.0.0.1", refresh.LastIpAddress);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenRefreshSessionCannotBeCreated()
    {
        var user = CreateUser();
        var repository = new FakeIdentityRepository(user);
        var refresh = new FakeRefreshTokenService(user) { CreateResult = null };
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.Succeeded), refresh);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.Null(result);
        Assert.Equal(1, repository.SuccessfulLoginCalls);
        Assert.Equal(1, refresh.CreateCalls);
    }

    [Fact]
    public async Task LoginAsync_RehashesPassword_WhenHasherRequestsUpgrade()
    {
        var repository = new FakeIdentityRepository(CreateUser());
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.SuccessRehashNeeded),
            new FakeRefreshTokenService(repository.User!));

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.NotNull(result);
        Assert.Equal(1, repository.PasswordHashUpdateCalls);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenAccountIsCurrentlyLocked()
    {
        var user = CreateUser() with { LockoutEndUtc = DateTime.UtcNow.AddMinutes(5) };
        var repository = new FakeIdentityRepository(user);
        var refresh = new FakeRefreshTokenService(user);
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.Succeeded), refresh);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.Null(result);
        Assert.Equal(0, repository.FailedLoginCalls);
        Assert.Equal(0, repository.SuccessfulLoginCalls);
        Assert.Equal(0, refresh.CreateCalls);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenMembershipLookupDoesNotFindUser()
    {
        var repository = new FakeIdentityRepository(null);
        var refresh = new FakeRefreshTokenService(null);
        var sut = CreateService(repository, new FakePasswordHasher(PasswordVerificationOutcome.Succeeded), refresh);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.Null(result);
        Assert.Equal(0, repository.FailedLoginCalls);
        Assert.Equal(0, refresh.CreateCalls);
    }

    private static AuthenticationService CreateService(
        FakeIdentityRepository repository,
        FakePasswordHasher hasher,
        FakeRefreshTokenService refresh) =>
        new(repository, hasher, refresh, TimeProvider.System);

    private static UserLoginRecord CreateUser() => new(
        Guid.NewGuid(), "Person", "person@example.com", "stored-hash", true, 0, null,
        Guid.NewGuid(), "ACME", "Acme Tenant", Guid.NewGuid());

    private sealed class FakeIdentityRepository(UserLoginRecord? user) : IUserIdentityRepository
    {
        public UserLoginRecord? User => user;
        public string? LastNormalizedEmail { get; private set; }
        public string? LastNormalizedTenantCode { get; private set; }
        public int FailedLoginCalls { get; private set; }
        public int SuccessfulLoginCalls { get; private set; }
        public int PasswordHashUpdateCalls { get; private set; }

        public Task<UserLoginRecord?> FindForLoginAsync(string normalizedEmail, string normalizedTenantCode,
            CancellationToken cancellationToken = default)
        {
            LastNormalizedEmail = normalizedEmail;
            LastNormalizedTenantCode = normalizedTenantCode;
            return Task.FromResult(user);
        }

        public Task RecordFailedLoginAsync(Guid userId, int maxFailedAttempts, TimeSpan lockoutDuration,
            CancellationToken cancellationToken = default)
        {
            FailedLoginCalls++;
            Assert.Equal(5, maxFailedAttempts);
            Assert.Equal(TimeSpan.FromMinutes(15), lockoutDuration);
            return Task.CompletedTask;
        }

        public Task RecordSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            SuccessfulLoginCalls++;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordHashAsync(Guid userId, string passwordHash,
            CancellationToken cancellationToken = default)
        {
            PasswordHashUpdateCalls++;
            Assert.Equal("new-hash", passwordHash);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher(PasswordVerificationOutcome outcome) : IPasswordHasher
    {
        public string HashPassword(string password) => "new-hash";
        public PasswordVerificationOutcome VerifyHashedPassword(string passwordHash, string providedPassword) => outcome;
    }

    private sealed class FakeRefreshTokenService(UserLoginRecord? user) : IRefreshTokenService
    {
        public int CreateCalls { get; private set; }
        public string? LastIpAddress { get; private set; }
        public RefreshSessionResult? CreateResult { get; set; } = user is null ? null :
            new RefreshSessionResult(user, new GeneratedAccessToken("test-token", DateTime.UtcNow.AddMinutes(10)),
                "test-refresh-token", DateTime.UtcNow.AddDays(14));

        public Task<RefreshSessionResult?> CreateSessionAsync(UserLoginRecord requestedUser, string? ipAddress,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastIpAddress = ipAddress;
            Assert.Equal(user?.UserId, requestedUser.UserId);
            return Task.FromResult(CreateResult);
        }

        public Task<RefreshTokenRefreshResult> RefreshAsync(string refreshToken, string? ipAddress,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RevokeAsync(string refreshToken, string? ipAddress, string reason = "Logout",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
