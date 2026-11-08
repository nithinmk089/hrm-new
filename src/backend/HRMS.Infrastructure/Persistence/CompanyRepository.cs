using Dapper;
using HRMS.Application.Companies;
using HRMS.Application.Abstractions.Persistence;

namespace HRMS.Infrastructure.Persistence;

internal sealed class CompanyRepository(IDbConnectionFactory connectionFactory) : ICompanyRepository
{
    private const string SelectColumns = """
        CompanyId,
        CompanyCode,
        LegalName,
        DisplayName,
        TaxRegistrationNumber,
        CountryCode,
        DefaultCurrencyCode,
        TimeZoneId,
        IsActive,
        CreatedAtUtc,
        UpdatedAtUtc,
        RowVersion
        """;

    public async Task<IReadOnlyList<CompanyDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                CompanyId, CompanyCode, LegalName, DisplayName,
                TaxRegistrationNumber, CountryCode, DefaultCurrencyCode,
                TimeZoneId, IsActive, CreatedAtUtc, UpdatedAtUtc, RowVersion
            FROM hrms.Companies
            WHERE DeletedAtUtc IS NULL
            ORDER BY DisplayName, CompanyCode;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CompanyDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM hrms.Companies
            WHERE CompanyId = @CompanyId
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto?> GetByCodeAsync(string companyCode, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM hrms.Companies
            WHERE CompanyCode = @CompanyCode
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(
            new CommandDefinition(sql, new { CompanyCode = companyCode }, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO hrms.Companies
            (
                CompanyCode, LegalName, DisplayName, TaxRegistrationNumber,
                CountryCode, DefaultCurrencyCode, TimeZoneId
            )
            OUTPUT
                INSERTED.CompanyId,
                INSERTED.CompanyCode,
                INSERTED.LegalName,
                INSERTED.DisplayName,
                INSERTED.TaxRegistrationNumber,
                INSERTED.CountryCode,
                INSERTED.DefaultCurrencyCode,
                INSERTED.TimeZoneId,
                INSERTED.IsActive,
                INSERTED.CreatedAtUtc,
                INSERTED.UpdatedAtUtc,
                INSERTED.RowVersion
            VALUES
            (
                @CompanyCode, @LegalName, @DisplayName, @TaxRegistrationNumber,
                @CountryCode, @DefaultCurrencyCode, @TimeZoneId
            );
            """;

        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<CompanyDto>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<CompanyDto?> UpdateAsync(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE hrms.Companies
            SET LegalName = @LegalName,
                DisplayName = @DisplayName,
                TaxRegistrationNumber = @TaxRegistrationNumber,
                CountryCode = @CountryCode,
                DefaultCurrencyCode = @DefaultCurrencyCode,
                TimeZoneId = @TimeZoneId,
                UpdatedAtUtc = SYSUTCDATETIME()
            OUTPUT
                INSERTED.CompanyId,
                INSERTED.CompanyCode,
                INSERTED.LegalName,
                INSERTED.DisplayName,
                INSERTED.TaxRegistrationNumber,
                INSERTED.CountryCode,
                INSERTED.DefaultCurrencyCode,
                INSERTED.TimeZoneId,
                INSERTED.IsActive,
                INSERTED.CreatedAtUtc,
                INSERTED.UpdatedAtUtc,
                INSERTED.RowVersion
            WHERE CompanyId = @CompanyId
              AND RowVersion = @RowVersion
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyDto>(
            new CommandDefinition(
                sql,
                new
                {
                    CompanyId = companyId,
                    request.LegalName,
                    request.DisplayName,
                    request.TaxRegistrationNumber,
                    request.CountryCode,
                    request.DefaultCurrencyCode,
                    request.TimeZoneId,
                    request.RowVersion
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(
        Guid companyId,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE hrms.Companies
            SET IsActive = 0,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CompanyId = @CompanyId
              AND RowVersion = @RowVersion
              AND DeletedAtUtc IS NULL;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { CompanyId = companyId, RowVersion = rowVersion },
                cancellationToken: cancellationToken));

        return affectedRows == 1;
    }
}
