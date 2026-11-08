using HRMS.Application.Designations;
using Xunit;

namespace HRMS.Infrastructure.Tests.Designations;

public sealed class DesignationServiceTests
{
    [Fact]
    public async Task GetAllAsync_RejectsEmptyTenantId()
    {
        var repository = new FakeDesignationRepository();
        var service = new DesignationService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAllAsync(Guid.Empty, Guid.NewGuid()));

        Assert.False(repository.GetAllCalled);
    }

    [Fact]
    public async Task CreateAsync_NormalizesCodeAndText_AndPreservesCompanyScope()
    {
        var repository = new FakeDesignationRepository();
        var service = new DesignationService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAsync(
            tenantId,
            companyId,
            new CreateDesignationRequest(" senior-dev ", " Senior Developer ", " Engineering ", 4));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.Equal(companyId, repository.CapturedCompanyId);
        var captured = Assert.IsType<CreateDesignationRequest>(repository.CapturedCreateRequest);
        Assert.Equal("SENIOR-DEV", captured.DesignationCode);
        Assert.Equal("Senior Developer", captured.DesignationName);
        Assert.Equal("Engineering", captured.Description);
        Assert.Equal((short)4, captured.JobLevel);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateDesignationCode()
    {
        var repository = new FakeDesignationRepository { ExistingCode = "MANAGER" };
        var service = new DesignationService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new CreateDesignationRequest("manager", "Manager", null, null)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)-1)]
    public async Task CreateAsync_RejectsNonPositiveJobLevel(short jobLevel)
    {
        var repository = new FakeDesignationRepository();
        var service = new DesignationService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new CreateDesignationRequest("DEV", "Developer", null, jobLevel)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleRowVersion()
    {
        var designationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var existing = CreateDto(designationId, companyId, "DEV", "Developer");
        var repository = new FakeDesignationRepository
        {
            ExistingDesignation = existing,
            UpdateReturnsNull = true
        };
        var service = new DesignationService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                Guid.NewGuid(),
                companyId,
                designationId,
                new UpdateDesignationRequest("Senior Developer", null, 4, new byte[8])));

        Assert.NotNull(repository.CapturedUpdateRequest);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenDesignationDoesNotExistInTenantScope()
    {
        var repository = new FakeDesignationRepository();
        var service = new DesignationService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateDesignationRequest("Developer", null, null, new byte[8]));

        Assert.Null(result);
        Assert.Null(repository.CapturedUpdateRequest);
    }

    private sealed class FakeDesignationRepository : IDesignationRepository
    {
        public bool GetAllCalled { get; private set; }
        public Guid? CapturedTenantId { get; private set; }
        public Guid? CapturedCompanyId { get; private set; }
        public CreateDesignationRequest? CapturedCreateRequest { get; private set; }
        public UpdateDesignationRequest? CapturedUpdateRequest { get; private set; }
        public string? ExistingCode { get; init; }
        public DesignationDto? ExistingDesignation { get; init; }
        public bool UpdateReturnsNull { get; init; }

        public Task<IReadOnlyList<DesignationDto>> GetAllAsync(
            Guid tenantId, Guid companyId, CancellationToken cancellationToken)
        {
            GetAllCalled = true;
            return Task.FromResult<IReadOnlyList<DesignationDto>>([]);
        }

        public Task<DesignationDto?> GetByIdAsync(
            Guid tenantId, Guid companyId, Guid designationId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingDesignation?.DesignationId == designationId
                ? ExistingDesignation
                : null);

        public Task<DesignationDto?> GetByCodeAsync(
            Guid tenantId, Guid companyId, string designationCode, CancellationToken cancellationToken) =>
            Task.FromResult<DesignationDto?>(ExistingCode == designationCode
                ? CreateDto(Guid.NewGuid(), companyId, designationCode, "Existing")
                : null);

        public Task<DesignationDto> CreateAsync(
            Guid tenantId, Guid companyId, CreateDesignationRequest request,
            CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCompanyId = companyId;
            CapturedCreateRequest = request;
            return Task.FromResult(CreateDto(
                Guid.NewGuid(), companyId, request.DesignationCode, request.DesignationName));
        }

        public Task<DesignationDto?> UpdateAsync(
            Guid tenantId, Guid companyId, Guid designationId,
            UpdateDesignationRequest request, CancellationToken cancellationToken)
        {
            CapturedUpdateRequest = request;
            return Task.FromResult<DesignationDto?>(UpdateReturnsNull
                ? null
                : CreateDto(designationId, companyId, "DEV", request.DesignationName));
        }

        public Task<bool> DeactivateAsync(
            Guid tenantId, Guid companyId, Guid designationId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private static DesignationDto CreateDto(
        Guid id, Guid companyId, string code, string name) =>
        new(id, companyId, code, name, null, null, true,
            DateTime.UtcNow, null, new byte[8]);
}
