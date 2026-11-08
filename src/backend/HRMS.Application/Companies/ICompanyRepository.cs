namespace HRMS.Application.Companies;

/// <summary>Persistence operations for company records.</summary>
public interface ICompanyRepository
{
    Task<IReadOnlyList<CompanyDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken);
    Task<CompanyDto?> GetByCodeAsync(string companyCode, CancellationToken cancellationToken);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken);
    Task<CompanyDto?> UpdateAsync(Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid companyId, byte[] rowVersion, CancellationToken cancellationToken);
}
