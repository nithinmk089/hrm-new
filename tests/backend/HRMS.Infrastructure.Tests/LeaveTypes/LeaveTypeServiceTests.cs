using HRMS.Application.LeaveTypes;
using Xunit;

namespace HRMS.Infrastructure.Tests.LeaveTypes;

public sealed class LeaveTypeServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesCodeAndText_AndPreservesScope()
    {
        var repository = new FakeLeaveTypeRepository();
        var service = new LeaveTypeService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAsync(tenantId, companyId,
            new CreateLeaveTypeRequest(" annual ", " Annual Leave ", " Paid leave ", true, true,
                20, true, true, 5));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.Equal(companyId, repository.CapturedCompanyId);
        var request = Assert.IsType<CreateLeaveTypeRequest>(repository.CapturedCreateRequest);
        Assert.Equal("ANNUAL", request.LeaveTypeCode);
        Assert.Equal("Annual Leave", request.LeaveTypeName);
        Assert.Equal("Paid leave", request.Description);
        Assert.Equal(20m, request.AnnualEntitlementDays);
        Assert.Equal(5m, request.MaxCarryForwardDays);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCode()
    {
        var repository = new FakeLeaveTypeRepository { ExistingCode = "ANNUAL" };
        var service = new LeaveTypeService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new CreateLeaveTypeRequest("annual", "Annual Leave", null, true, true, 20, false, false, 0)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task CreateAsync_RejectsInvalidCarryForward(bool allowCarryForward, decimal maxCarryForwardDays)
    {
        var repository = new FakeLeaveTypeRepository();
        var service = new LeaveTypeService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new CreateLeaveTypeRequest("ANNUAL", "Annual Leave", null, true, true, 20,
                false, allowCarryForward, maxCarryForwardDays)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleRowVersion()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var repository = new FakeLeaveTypeRepository
        {
            ExistingLeaveType = CreateDto(id, companyId),
            UpdateReturnsNull = true
        };
        var service = new LeaveTypeService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            Guid.NewGuid(), companyId, id,
            new UpdateLeaveTypeRequest("Annual Leave", null, true, true, 20, false, false, 0, new byte[8])));

        Assert.NotNull(repository.CapturedUpdateRequest);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenLeaveTypeDoesNotExist()
    {
        var repository = new FakeLeaveTypeRepository();
        var service = new LeaveTypeService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new UpdateLeaveTypeRequest("Annual Leave", null, true, true, 20, false, false, 0, new byte[8]));

        Assert.Null(result);
        Assert.Null(repository.CapturedUpdateRequest);
    }

    private sealed class FakeLeaveTypeRepository : ILeaveTypeRepository
    {
        public Guid? CapturedTenantId { get; private set; }
        public Guid? CapturedCompanyId { get; private set; }
        public CreateLeaveTypeRequest? CapturedCreateRequest { get; private set; }
        public UpdateLeaveTypeRequest? CapturedUpdateRequest { get; private set; }
        public string? ExistingCode { get; init; }
        public LeaveTypeDto? ExistingLeaveType { get; init; }
        public bool UpdateReturnsNull { get; init; }

        public Task<IReadOnlyList<LeaveTypeDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LeaveTypeDto>>([]);

        public Task<LeaveTypeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingLeaveType?.LeaveTypeId == leaveTypeId ? ExistingLeaveType : null);

        public Task<LeaveTypeDto?> GetByCodeAsync(Guid tenantId, Guid companyId, string leaveTypeCode, CancellationToken cancellationToken) =>
            Task.FromResult<LeaveTypeDto?>(ExistingCode == leaveTypeCode
                ? CreateDto(Guid.NewGuid(), companyId)
                : null);

        public Task<LeaveTypeDto> CreateAsync(Guid tenantId, Guid companyId, CreateLeaveTypeRequest request, CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCompanyId = companyId;
            CapturedCreateRequest = request;
            return Task.FromResult(CreateDto(Guid.NewGuid(), companyId));
        }

        public Task<LeaveTypeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, UpdateLeaveTypeRequest request, CancellationToken cancellationToken)
        {
            CapturedUpdateRequest = request;
            return Task.FromResult<LeaveTypeDto?>(UpdateReturnsNull ? null : CreateDto(leaveTypeId, companyId));
        }

        public Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid leaveTypeId, byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private static LeaveTypeDto CreateDto(Guid id, Guid companyId) =>
        new(id, companyId, "ANNUAL", "Annual Leave", null, true, true, 20, true, true, 5,
            true, DateTime.UtcNow, null, new byte[8]);
}
