using Dapper;
using HRMS.Application.Companies;
using HRMS.Application.Abstractions.Persistence;
namespace HRMS.Infrastructure.Persistence;
internal sealed class CompanyRepository(IDbConnectionFactory connectionFactory) : ICompanyRepository
{
    private const string SelectColumns = """
        CompanyId, CompanyCode, LegalName, DisplayName, TaxRegistrationNumber,
        CountryCode, DefaultCurrencyCode, TimeZoneId, IsActive, CreatedAtUtc,
        UpdatedAtUtc, RowVersion
        """;

    public async Task<IReadOnlyList<CompanyDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns} FROM hrms.Companies
            WHERE TenantId = @TenantId AND DeletedAtUtc IS NULL
            ORDER BY DisplayName, CompanyCode;
            """;
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CompanyDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns} FROM hrms.Companies
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND DeletedAtUtc IS NULL;
            """;
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(new CommandDefinition(sql, new { TenantId = tenantId, CompanyId = companyId }, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto?> GetByCodeAsync(Guid tenantId, string companyCode, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns} FROM hrms.Companies
            WHERE TenantId = @TenantId AND CompanyCode = @CompanyCode AND DeletedAtUtc IS NULL;
            """;
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(new CommandDefinition(sql, new { TenantId = tenantId, CompanyCode = companyCode }, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto> CreateAsync(Guid tenantId, CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO hrms.Companies
                (TenantId, CompanyCode, LegalName, DisplayName, TaxRegistrationNumber, CountryCode, DefaultCurrencyCode, TimeZoneId)
            OUTPUT INSERTED.CompanyId, INSERTED.CompanyCode, INSERTED.LegalName, INSERTED.DisplayName,
                INSERTED.TaxRegistrationNumber, INSERTED.CountryCode, INSERTED.DefaultCurrencyCode,
                INSERTED.TimeZoneId, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
            VALUES
                (@TenantId, @CompanyCode, @LegalName, @DisplayName, @TaxRegistrationNumber, @CountryCode, @DefaultCurrencyCode, @TimeZoneId);
            """;
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<CompanyDto>(new CommandDefinition(sql, new {
            TenantId = tenantId, request.CompanyCode, request.LegalName, request.DisplayName,
            request.TaxRegistrationNumber, request.CountryCode, request.DefaultCurrencyCode, request.TimeZoneId
        }, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto?> UpdateAsync(Guid tenantId, Guid companyId, UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE hrms.Companies
            SET LegalName = @LegalName, DisplayName = @DisplayName,
                TaxRegistrationNumber = @TaxRegistrationNumber, CountryCode = @CountryCode,
                DefaultCurrencyCode = @DefaultCurrencyCode, TimeZoneId = @TimeZoneId,
                UpdatedAtUtc = SYSUTCDATETIME()
            OUTPUT INSERTED.CompanyId, INSERTED.CompanyCode, INSERTED.LegalName, INSERTED.DisplayName,
                INSERTED.TaxRegistrationNumber, INSERTED.CountryCode, INSERTED.DefaultCurrencyCode,
                INSERTED.TimeZoneId, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND RowVersion = @RowVersion AND DeletedAtUtc IS NULL;
            """;
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(new CommandDefinition(sql, new {
            TenantId = tenantId, CompanyId = companyId, request.LegalName, request.DisplayName,
            request.TaxRegistrationNumber, request.CountryCode, request.DefaultCurrencyCode,
            request.TimeZoneId, request.RowVersion
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE hrms.Companies SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND CompanyId = @CompanyId
              AND RowVersion = @RowVersion AND DeletedAtUtc IS NULL;
            """;
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, CompanyId = companyId, RowVersion = rowVersion },
            cancellationToken: cancellationToken));
        return affected == 1;
    }
}