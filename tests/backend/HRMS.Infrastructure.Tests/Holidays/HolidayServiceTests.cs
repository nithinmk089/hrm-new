using HRMS.Application.Holidays;
using Xunit;

namespace HRMS.Infrastructure.Tests.Holidays;

public sealed class HolidayServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesNameAndDate_AndPreservesScope()
    {
        var repository = new FakeHolidayRepository();
        var service = new HolidayService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAsync(tenantId, companyId,
            new CreateHolidayRequest(" New Year's Day ", new DateTime(2027, 1, 1, 15, 30, 0), " Public holiday ", false));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.Equal(companyId, repository.CapturedCompanyId);
        var request = Assert.IsType<CreateHolidayRequest>(repository.CapturedCreateRequest);
        Assert.Equal("New Year's Day", request.HolidayName);
        Assert.Equal(new DateTime(2027, 1, 1), request.HolidayDate);
        Assert.Equal("Public holiday", request.Description);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateDate()
    {
        var date = new DateTime(2027, 1, 1);
        var repository = new FakeHolidayRepository { ExistingDate = CreateDto(Guid.NewGuid(), Guid.NewGuid(), date) };
        var service = new HolidayService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(), new CreateHolidayRequest("New Year", date, null, false)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleRowVersion()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var repository = new FakeHolidayRepository
        {
            ExistingHoliday = CreateDto(id, companyId, new DateTime(2027, 1, 1)),
            UpdateReturnsNull = true
        };
        var service = new HolidayService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(
            Guid.NewGuid(), companyId, id,
            new UpdateHolidayRequest("New Year", new DateTime(2027, 1, 1), null, false, new byte[8])));

        Assert.NotNull(repository.CapturedUpdateRequest);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenHolidayDoesNotExist()
    {
        var repository = new FakeHolidayRepository();
        var service = new HolidayService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new UpdateHolidayRequest("Holiday", new DateTime(2027, 1, 1), null, false, new byte[8]));

        Assert.Null(result);
        Assert.Null(repository.CapturedUpdateRequest);
    }

    private sealed class FakeHolidayRepository : IHolidayRepository
    {
        public Guid? CapturedTenantId { get; private set; }
        public Guid? CapturedCompanyId { get; private set; }
        public CreateHolidayRequest? CapturedCreateRequest { get; private set; }
        public UpdateHolidayRequest? CapturedUpdateRequest { get; private set; }
        public HolidayDto? ExistingDate { get; init; }
        public HolidayDto? ExistingHoliday { get; init; }
        public bool UpdateReturnsNull { get; init; }

        public Task<IReadOnlyList<HolidayDto>> GetAllAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HolidayDto>>([]);

        public Task<HolidayDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid holidayId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingHoliday?.HolidayId == holidayId ? ExistingHoliday : null);

        public Task<HolidayDto?> GetByDateAsync(Guid tenantId, Guid companyId, DateTime holidayDate, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingDate?.HolidayDate.Date == holidayDate.Date ? ExistingDate : null);

        public Task<HolidayDto> CreateAsync(Guid tenantId, Guid companyId, CreateHolidayRequest request, CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCompanyId = companyId;
            CapturedCreateRequest = request;
            return Task.FromResult(CreateDto(Guid.NewGuid(), companyId, request.HolidayDate));
        }

        public Task<HolidayDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid holidayId, UpdateHolidayRequest request, CancellationToken cancellationToken)
        {
            CapturedUpdateRequest = request;
            return Task.FromResult<HolidayDto?>(UpdateReturnsNull ? null : CreateDto(holidayId, companyId, request.HolidayDate));
        }

        public Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid holidayId, byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private static HolidayDto CreateDto(Guid id, Guid companyId, DateTime date) =>
        new(id, companyId, "Holiday", date.Date, null, false, true, DateTime.UtcNow, null, new byte[8]);
}
