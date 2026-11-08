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
        var hasher = new FakePasswordHasher(PasswordVerificationOutcome.Failed);
        var tokenGenerator = new FakeAccessTokenGenerator();
        var sut = new AuthenticationService(repository, hasher, tokenGenerator, TimeProvider.System);

        var result = await sut.LoginAsync(new LoginRequest(" person@example.com ", "wrong", " acme "));

        Assert.Null(result);
        Assert.Equal("PERSON@EXAMPLE.COM", repository.LastNormalizedEmail);
        Assert.Equal("ACME", repository.LastNormalizedTenantCode);
        Assert.Equal(1, repository.FailedLoginCalls);
        Assert.Equal(0, repository.SuccessfulLoginCalls);
        Assert.Equal(0, tokenGenerator.GenerateCalls);
    }

    [Fact]
    public async Task LoginAsync_ReturnsBearerToken_AndRecordsSuccess_ForValidCredentials()
    {
        var user = CreateUser();
        var repository = new FakeIdentityRepository(user);
        var hasher = new FakePasswordHasher(PasswordVerificationOutcome.Succeeded);
        var tokenGenerator = new FakeAccessTokenGenerator();
        var sut = new AuthenticationService(repository, hasher, tokenGenerator, TimeProvider.System);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("test-token", result.AccessToken);
        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(user.TenantId, result.TenantId);
        Assert.Equal(1, repository.SuccessfulLoginCalls);
        Assert.Equal(1, tokenGenerator.GenerateCalls);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenMembershipLookupDoesNotFindUser()
    {
        var repository = new FakeIdentityRepository(null);
        var sut = new AuthenticationService(
            repository,
            new FakePasswordHasher(PasswordVerificationOutcome.Succeeded),
            new FakeAccessTokenGenerator(),
            TimeProvider.System);

        var result = await sut.LoginAsync(new LoginRequest("person@example.com", "correct", "ACME"));

        Assert.Null(result);
        Assert.Equal(0, repository.FailedLoginCalls);
    }

    private static UserLoginRecord CreateUser() => new(
        Guid.NewGuid(),
        "Person",
        "person@example.com",
        "stored-hash",
        true,
        0,
        null,
        Guid.NewGuid(),
        "ACME",
        "Acme Tenant",
        Guid.NewGuid());

    private sealed class FakeIdentityRepository(UserLoginRecord? user) : IUserIdentityRepository
    {
        public string? LastNormalizedEmail { get; private set; }
        public string? LastNormalizedTenantCode { get; private set; }
        public int FailedLoginCalls { get; private set; }
        public int SuccessfulLoginCalls { get; private set; }

        public Task<UserLoginRecord?> FindForLoginAsync(
            string normalizedEmail,
            string normalizedTenantCode,
            CancellationToken cancellationToken = default)
        {
            LastNormalizedEmail = normalizedEmail;
            LastNormalizedTenantCode = normalizedTenantCode;
            return Task.FromResult(user);
        }

        public Task RecordFailedLoginAsync(
            Guid userId,
            int maxFailedAttempts,
            TimeSpan lockoutDuration,
            CancellationToken cancellationToken = default)
        {
            FailedLoginCalls++;
            Assert.Equal(5, maxFailedAttempts);
            Assert.Equal(TimeSpan.FromMinutes(15), lockoutDuration);
            return Task.CompletedTask;
        }

        public Task RecordSuccessfulLoginAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            SuccessfulLoginCalls++;
            return Task.CompletedTask;
        }

        public Task UpdatePasswordHashAsync(
            Guid userId,
            string passwordHash,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher(PasswordVerificationOutcome outcome) : IPasswordHasher
    {
        public string HashPassword(string password) => "new-hash";

        public PasswordVerificationOutcome VerifyHashedPassword(
            string passwordHash,
            string providedPassword) => outcome;
    }

    private sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
    {
        public int GenerateCalls { get; private set; }

        public GeneratedAccessToken Generate(UserLoginRecord user)
        {
            GenerateCalls++;
            return new GeneratedAccessToken("test-token", DateTime.UtcNow.AddMinutes(10));
        }
    }
}
