namespace HRMS.Application.Companies;
/// <summary>Tenant-scoped use cases for managing HRMS companies.</summary>
public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<CompanyDto?> GetByIdAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateAsync(Guid tenantId, CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyDto?> UpdateAsync(Guid tenantId, Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, byte[] rowVersion, CancellationToken cancellationToken = default);
}