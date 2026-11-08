namespace HRMS.Application.Companies;
/// <summary>Persistence operations for tenant-scoped company records.</summary>
public interface ICompanyRepository
{
    Task<IReadOnlyList<CompanyDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<CompanyDto?> GetByIdAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
    Task<CompanyDto?> GetByCodeAsync(Guid tenantId, string companyCode, CancellationToken cancellationToken);
    Task<CompanyDto> CreateAsync(Guid tenantId, CreateCompanyRequest request, CancellationToken cancellationToken);
    Task<CompanyDto?> UpdateAsync(Guid tenantId, Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, byte[] rowVersion, CancellationToken cancellationToken);
}