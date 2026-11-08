namespace HRMS.Application.Companies;

/// <summary>Use cases for managing HRMS companies.</summary>
public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyDto?> UpdateAsync(Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid companyId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
