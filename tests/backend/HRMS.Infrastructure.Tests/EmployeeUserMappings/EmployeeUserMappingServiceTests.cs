using HRMS.Application.EmployeeUserMappings;
using Xunit;

namespace HRMS.Infrastructure.Tests.EmployeeUserMappings;

public sealed class EmployeeUserMappingServiceTests
{
    [Fact]
    public async Task CreateAsync_PreservesAuthenticatedScopeAndIds()
    {
        var repository = new FakeRepository();
        var service = new EmployeeUserMappingService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.CreateAsync(tenantId, companyId, new CreateEmployeeUserMappingRequest(employeeId, userId));

        Assert.Equal(tenantId, repository.TenantId);
        Assert.Equal(companyId, repository.CompanyId);
        Assert.Equal(employeeId, repository.Request!.EmployeeId);
        Assert.Equal(userId, repository.Request.UserId);
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyEmployeeId()
    {
        var service = new EmployeeUserMappingService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new CreateEmployeeUserMappingRequest(Guid.Empty, Guid.NewGuid())));
    }

    [Fact]
    public async Task DeactivateAsync_RequiresEightByteRowVersion()
    {
        var service = new EmployeeUserMappingService(new FakeRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeactivateAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [1, 2]));
    }

    private sealed class FakeRepository : IEmployeeUserMappingRepository
    {
        public Guid? TenantId { get; private set; }
        public Task<IReadOnlyList<AvailableTenantUserDto>> GetAvailableTenantUsersAsync(Guid tenantId, Guid companyId, string? search, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AvailableTenantUserDto>>([]);
        public Guid? CompanyId { get; private set; }
        public CreateEmployeeUserMappingRequest? Request { get; private set; }

        public Task<EmployeeUserMappingDto?> GetByEmployeeAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult<EmployeeUserMappingDto?>(null);

        public Task<EmployeeUserMappingDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken)
        {
            TenantId = tenantId;
            CompanyId = companyId;
            Request = request;
            return Task.FromResult(new EmployeeUserMappingDto(Guid.NewGuid(), companyId, request.EmployeeId, request.UserId, true, DateTime.UtcNow, null, new byte[8]));
        }

        public Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid mappingId, byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
