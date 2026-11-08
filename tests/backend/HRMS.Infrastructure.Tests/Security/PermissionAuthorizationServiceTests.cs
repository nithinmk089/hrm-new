using HRMS.Application.Abstractions.Security;
using HRMS.Application.Security;
using Xunit;

namespace HRMS.Infrastructure.Tests.Security;

public sealed class PermissionAuthorizationServiceTests
{
    [Fact]
    public async Task HasPermissionAsync_DelegatesValidatedTenantMembershipAndScope()
    {
        var repository = new FakePermissionRepository(true);
        var now = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
        var sut = new PermissionAuthorizationService(repository, new FixedTimeProvider(now));
        var request = new PermissionAuthorizationRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " employees.read ",
            Guid.NewGuid(), Guid.NewGuid());

        var result = await sut.HasPermissionAsync(request);

        Assert.True(result);
        Assert.NotNull(repository.Request);
        Assert.Equal(request.UserId, repository.Request.UserId);
        Assert.Equal(request.TenantId, repository.Request.TenantId);
        Assert.Equal(request.MembershipId, repository.Request.MembershipId);
        Assert.Equal("employees.read", repository.Request.PermissionCode);
        Assert.Equal(request.CompanyId, repository.Request.CompanyId);
        Assert.Equal(request.OfficeId, repository.Request.OfficeId);
        Assert.Equal(now.UtcDateTime, repository.NowUtc);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenRepositoryDeniesGrant()
    {
        var repository = new FakePermissionRepository(false);
        var sut = new PermissionAuthorizationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await sut.HasPermissionAsync(CreateRequest());

        Assert.False(result);
    }

    [Fact]
    public async Task HasPermissionAsync_RejectsOfficeWithoutCompany()
    {
        var repository = new FakePermissionRepository(true);
        var sut = new PermissionAuthorizationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var request = CreateRequest() with { OfficeId = Guid.NewGuid() };

        await Assert.ThrowsAsync<ArgumentException>(() => sut.HasPermissionAsync(request));
        Assert.Null(repository.Request);
    }

    [Fact]
    public async Task HasPermissionAsync_RejectsEmptyIdentityIds()
    {
        var repository = new FakePermissionRepository(true);
        var sut = new PermissionAuthorizationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var request = CreateRequest() with { TenantId = Guid.Empty };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.HasPermissionAsync(request));
        Assert.Null(repository.Request);
    }

    [Fact]
    public async Task HasPermissionAsync_RejectsBlankPermissionCode()
    {
        var repository = new FakePermissionRepository(true);
        var sut = new PermissionAuthorizationService(repository, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var request = CreateRequest() with { PermissionCode = " " };

        await Assert.ThrowsAsync<ArgumentException>(() => sut.HasPermissionAsync(request));
        Assert.Null(repository.Request);
    }

    private static PermissionAuthorizationRequest CreateRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "employees.read");

    private sealed class FakePermissionRepository(bool result) : IPermissionAuthorizationRepository
    {
        public PermissionAuthorizationRequest? Request { get; private set; }
        public DateTime? NowUtc { get; private set; }

        public Task<bool> HasPermissionAsync(
            PermissionAuthorizationRequest request,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            NowUtc = nowUtc;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
