using HRMS.Application.Companies;
using Xunit;

namespace HRMS.Infrastructure.Tests.Companies;

public sealed class CompanyServiceTests
{
    [Fact]
    public async Task GetAllAsync_RejectsEmptyTenantId()
    {
        var repository = new FakeCompanyRepository();
        var service = new CompanyService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllAsync(Guid.Empty));
        Assert.False(repository.GetAllCalled);
    }

    [Fact]
    public async Task CreateAsync_UsesTenantIdAndNormalizesRequest()
    {
        var repository = new FakeCompanyRepository();
        var service = new CompanyService(repository);
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(tenantId, new CreateCompanyRequest(
            " acme ", " Acme Legal Ltd ", " Acme ", null, "in", "inr", "Asia/Kolkata"));

        Assert.Equal(tenantId, repository.CapturedTenantId);
        Assert.NotNull(repository.CapturedCreateRequest);
        Assert.Equal("ACME", repository.CapturedCreateRequest!.CompanyCode);
        Assert.Equal("Acme Legal Ltd", repository.CapturedCreateRequest.LegalName);
        Assert.Equal("IN", repository.CapturedCreateRequest.CountryCode);
        Assert.Equal("INR", repository.CapturedCreateRequest.DefaultCurrencyCode);
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public bool GetAllCalled { get; private set; }
        public Guid? CapturedTenantId { get; private set; }
        public CreateCompanyRequest? CapturedCreateRequest { get; private set; }

        public Task<IReadOnlyList<CompanyDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            GetAllCalled = true;
            return Task.FromResult<IReadOnlyList<CompanyDto>>([]);
        }

        public Task<CompanyDto?> GetByIdAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<CompanyDto?>(null);

        public Task<CompanyDto?> GetByCodeAsync(Guid tenantId, string companyCode, CancellationToken cancellationToken) =>
            Task.FromResult<CompanyDto?>(null);

        public Task<CompanyDto> CreateAsync(Guid tenantId, CreateCompanyRequest request, CancellationToken cancellationToken)
        {
            CapturedTenantId = tenantId;
            CapturedCreateRequest = request;
            return Task.FromResult(new CompanyDto(
                Guid.NewGuid(), request.CompanyCode, request.LegalName, request.DisplayName,
                request.TaxRegistrationNumber, request.CountryCode, request.DefaultCurrencyCode,
                request.TimeZoneId, true, DateTime.UtcNow, null, new byte[8]));
        }

        public Task<CompanyDto?> UpdateAsync(Guid tenantId, Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<CompanyDto?>(null);

        public Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, byte[] rowVersion, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
