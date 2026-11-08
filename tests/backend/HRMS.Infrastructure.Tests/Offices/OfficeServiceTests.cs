using HRMS.Application.Offices;
using Xunit;

namespace HRMS.Infrastructure.Tests.Offices;

public sealed class OfficeServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesCodeCountryAndOptionalText()
    {
        var repository = new FakeOfficeRepository();
        var service = new OfficeService(repository);
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        await service.CreateAsync(tenantId, companyId,
            new CreateOfficeRequest(
                " hq ", " Head Office ", "  1 Main Street  ", null,
                " Kochi ", " Kerala ", " 682001 ", "in", " Asia/Kolkata ", true));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.Equal(companyId, repository.CapturedCompanyId);
        var captured = repository.CapturedCreateRequest;
        Assert.NotNull(captured);
        Assert.Equal("HQ", captured.OfficeCode);
        Assert.Equal("Head Office", captured.OfficeName);
        Assert.Equal("IN", captured.CountryCode);
        Assert.Equal("1 Main Street", captured.AddressLine1);
        Assert.Equal("Kochi", captured.City);
        Assert.Equal("Asia/Kolkata", captured.TimeZoneId);
        Assert.True(captured.IsHeadOffice);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateOfficeCode()
    {
        var repository = new FakeOfficeRepository { ExistingCode = "HQ" };
        var service = new OfficeService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateOfficeRequest("hq", "Head Office", null, null, null, null,
                    null, "IN", null, true)));

        Assert.Null(repository.CapturedCreateRequest);
    }

    [Theory]
    [InlineData("I")]
    [InlineData("IND")]
    [InlineData("1N")]
    [InlineData("")]
    public async Task CreateAsync_RejectsInvalidCountryCode(string countryCode)
    {
        var service = new OfficeService(new FakeOfficeRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(),
                new CreateOfficeRequest("HQ", "Head Office", null, null, null, null,
                    null, countryCode, null, true)));
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidRowVersion()
    {
        var service = new OfficeService(new FakeOfficeRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                new UpdateOfficeRequest("Head Office", null, null, null, null, null,
                    "IN", null, true, [1, 2, 3])));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenOfficeDoesNotExist()
    {
        var repository = new FakeOfficeRepository();
        var service = new OfficeService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new UpdateOfficeRequest("Head Office", null, null, null, null, null,
                "IN", null, true, new byte[8]));

        Assert.Null(result);
        Assert.Null(repository.CapturedUpdateRequest);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleRowVersion()
    {
        var officeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var repository = new FakeOfficeRepository
        {
            ExistingOffice = CreateOffice(officeId, companyId),
            UpdateReturnsNull = true
        };
        var service = new OfficeService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(Guid.NewGuid(), companyId, officeId,
                new UpdateOfficeRequest("Head Office", null, null, null, null, null,
                    "IN", null, true, new byte[8])));

        Assert.NotNull(repository.CapturedUpdateRequest);
    }

    private static OfficeDto CreateOffice(Guid officeId, Guid companyId) =>
        new(officeId, companyId, "HQ", "Head Office", null, null, null, null,
            null, "IN", null, true, true, DateTime.UtcNow, null, new byte[8]);

    private sealed class FakeOfficeRepository : IOfficeRepository
    {
        public Guid? CapturedTenantId { get; private set; }
        public Guid? CapturedCompanyId { get; private set; }
        public CreateOfficeRequest? CapturedCreateRequest { get; private set; }
        public UpdateOfficeRequest? CapturedUpdateRequest { get; private set; }
        public string? ExistingCode { get; init; }
        public OfficeDto? ExistingOffice { get; init; }
        public bool UpdateReturnsNull { get; init; }

        public Task<IReadOnlyList<OfficeDto>> GetAllAsync(
            Guid tenantId, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OfficeDto>>([]);

        public Task<OfficeDto?> GetByIdAsync(
            Guid tenantId, Guid companyId, Guid officeId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingOffice?.OfficeId == officeId ? ExistingOffice : null);

        public Task<OfficeDto?> GetByCodeAsync(
            Guid tenantId, Guid companyId, string officeCode, CancellationToken cancellationToken) =>
            Task.FromResult<OfficeDto?>(ExistingCode == officeCode
                ? CreateOffice(Guid.NewGuid(), companyId)
                : null);

        public Task<OfficeDto> CreateAsync(
            Guid tenantId, Guid companyId, CreateOfficeRequest request, CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCompanyId = companyId;
            CapturedCreateRequest = request;
            return Task.FromResult(CreateOffice(Guid.NewGuid(), companyId));
        }

        public Task<OfficeDto?> UpdateAsync(
            Guid tenantId, Guid companyId, Guid officeId,
            UpdateOfficeRequest request, CancellationToken cancellationToken)
        {
            CapturedUpdateRequest = request;
            return Task.FromResult<OfficeDto?>(UpdateReturnsNull
                ? null
                : CreateOffice(officeId, companyId));
        }

        public Task<bool> DeactivateAsync(
            Guid tenantId, Guid companyId, Guid officeId,
            byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
