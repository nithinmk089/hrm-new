using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Offices;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Calls tenant-scoped office stored procedures.</summary>
internal sealed class OfficeRepository(IDbConnectionFactory connectionFactory) : IOfficeRepository
{
    public async Task<IReadOnlyList<OfficeDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<OfficeDto>(new CommandDefinition(
            "hrms.usp_Offices_GetAll",
            new { TenantId = tenantId, CompanyId = companyId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<OfficeDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid officeId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OfficeDto>(new CommandDefinition(
            "hrms.usp_Offices_GetById",
            new { TenantId = tenantId, CompanyId = companyId, OfficeId = officeId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<OfficeDto?> GetByCodeAsync(
        Guid tenantId, Guid companyId, string officeCode, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OfficeDto>(new CommandDefinition(
            "hrms.usp_Offices_GetByCode",
            new { TenantId = tenantId, CompanyId = companyId, OfficeCode = officeCode },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<OfficeDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateOfficeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<OfficeDto>(new CommandDefinition(
                "hrms.usp_Offices_Create",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    request.OfficeCode,
                    request.OfficeName,
                    request.AddressLine1,
                    request.AddressLine2,
                    request.City,
                    request.StateOrProvince,
                    request.PostalCode,
                    request.CountryCode,
                    request.TimeZoneId,
                    request.IsHeadOffice
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                exception.Message.Contains("HeadOffice", StringComparison.OrdinalIgnoreCase)
                    ? "The company already has an active head office."
                    : $"Office code '{request.OfficeCode}' is already in use.",
                exception);
        }
        catch (SqlException exception) when (exception.Number == 51211)
        {
            throw new InvalidOperationException(
                "The company does not exist, is inactive, or is outside the authenticated tenant.", exception);
        }
    }

    public async Task<OfficeDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid officeId,
        UpdateOfficeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<OfficeDto>(new CommandDefinition(
                "hrms.usp_Offices_Update",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    OfficeId = officeId,
                    request.OfficeName,
                    request.AddressLine1,
                    request.AddressLine2,
                    request.City,
                    request.StateOrProvince,
                    request.PostalCode,
                    request.CountryCode,
                    request.TimeZoneId,
                    request.IsHeadOffice,
                    RowVersion = request.RowVersion
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("The company already has another active head office.", exception);
        }
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid officeId,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_Offices_Deactivate",
            new
            {
                TenantId = tenantId,
                CompanyId = companyId,
                OfficeId = officeId,
                RowVersion = rowVersion
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return affected == 1;
    }
}
