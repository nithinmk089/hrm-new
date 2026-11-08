using HRMS.Application.Departments;
using Xunit;

namespace HRMS.Infrastructure.Tests.Departments;

public sealed class DepartmentServiceTests
{
    [Fact]
    public async Task GetAllAsync_RejectsEmptyTenantId()
    {
        var repository = new FakeDepartmentRepository();
        var service = new DepartmentService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAllAsync(Guid.Empty, Guid.NewGuid()));

        Assert.False(repository.GetAllCalled);
    }

    [Fact]
    public async Task CreateAsync_NormalizesCodeAndText_AndUsesAuthenticatedTenantScope()
    {
        var repository = new FakeDepartmentRepository();
        var service = new DepartmentService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAsync(tenantId, companyId,
            new CreateDepartmentRequest(" finance ", " Finance ", " Shared services ", null));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.Equal(companyId, repository.CapturedCompanyId);
        Assert.NotNull(repository.CapturedCreateRequest);
        Assert.Equal("FINANCE", repository.CapturedCreateRequest!.DepartmentCode);
        Assert.Equal("Finance", repository.CapturedCreateRequest.DepartmentName);
        Assert.Equal("Shared services", repository.CapturedCreateRequest.Description);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateDepartmentCode()
    {
        var repository = new FakeDepartmentRepository { ExistingCode = "FINANCE" };
        var service = new DepartmentService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateDepartmentRequest("finance", "Finance", null, null)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Fact]
    public async Task CreateAsync_RejectsParentOutsideActiveCompanyScope()
    {
        var repository = new FakeDepartmentRepository { ParentExists = false };
        var service = new DepartmentService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateDepartmentRequest("FIN", "Finance", null, Guid.NewGuid())));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleRowVersion()
    {
        var departmentId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var existing = new DepartmentDto(
            departmentId, companyId, "FINANCE", "Finance", null, null,
            true, DateTime.UtcNow, null, new byte[8]);
        var repository = new FakeDepartmentRepository
        {
            ExistingDepartment = existing,
            UpdateReturnsNull = true
        };
        var service = new DepartmentService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(Guid.NewGuid(), companyId, departmentId,
                new UpdateDepartmentRequest("Finance & Accounts", null, null, new byte[8])));

        Assert.NotNull(repository.CapturedUpdateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsSelfParent()
    {
        var repository = new FakeDepartmentRepository();
        var service = new DepartmentService(repository);
        var departmentId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), departmentId,
                new UpdateDepartmentRequest("Finance", null, departmentId, new byte[8])));

        Assert.Null(repository.CapturedUpdateRequest);
    }

    private sealed class FakeDepartmentRepository : IDepartmentRepository
    {
        public bool GetAllCalled { get; private set; }
        public Guid? CapturedTenantId { get; private set; }
        public Guid? CapturedCompanyId { get; private set; }
        public CreateDepartmentRequest? CapturedCreateRequest { get; private set; }
        public UpdateDepartmentRequest? CapturedUpdateRequest { get; private set; }
        public string? ExistingCode { get; init; }
        public bool ParentExists { get; init; } = true;
        public DepartmentDto? ExistingDepartment { get; init; }
        public bool UpdateReturnsNull { get; init; }

        public Task<IReadOnlyList<DepartmentDto>> GetAllAsync(
            Guid tenantId, Guid companyId, CancellationToken cancellationToken)
        {
            GetAllCalled = true;
            return Task.FromResult<IReadOnlyList<DepartmentDto>>([]);
        }

        public Task<DepartmentDto?> GetByIdAsync(
            Guid tenantId, Guid companyId, Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingDepartment?.DepartmentId == departmentId ? ExistingDepartment : null);

        public Task<DepartmentDto?> GetByCodeAsync(
            Guid tenantId, Guid companyId, string departmentCode, CancellationToken cancellationToken) =>
            Task.FromResult<DepartmentDto?>(ExistingCode == departmentCode
                ? CreateDto(Guid.NewGuid(), companyId, departmentCode, "Existing")
                : null);

        public Task<bool> ParentExistsAsync(
            Guid tenantId, Guid companyId, Guid parentDepartmentId, CancellationToken cancellationToken) =>
            Task.FromResult(ParentExists);

        public Task<DepartmentDto> CreateAsync(
            Guid tenantId, Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCompanyId = companyId;
            CapturedCreateRequest = request;
            return Task.FromResult(CreateDto(Guid.NewGuid(), companyId, request.DepartmentCode, request.DepartmentName));
        }

        public Task<DepartmentDto?> UpdateAsync(
            Guid tenantId, Guid companyId, Guid departmentId,
            UpdateDepartmentRequest request, CancellationToken cancellationToken)
        {
            CapturedUpdateRequest = request;
            return Task.FromResult<DepartmentDto?>(UpdateReturnsNull
                ? null
                : CreateDto(departmentId, companyId, "FINANCE", request.DepartmentName));
        }

        public Task<bool> DeactivateAsync(
            Guid tenantId, Guid companyId, Guid departmentId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        private static DepartmentDto CreateDto(
            Guid id, Guid companyId, string code, string name) =>
            new(id, companyId, code, name, null, null, true,
                DateTime.UtcNow, null, new byte[8]);
    }
}
