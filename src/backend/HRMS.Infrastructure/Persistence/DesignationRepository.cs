using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Designations;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Calls tenant-scoped designation stored procedures.</summary>
internal sealed class DesignationRepository(IDbConnectionFactory connectionFactory)
    : IDesignationRepository
{
    public async Task<IReadOnlyList<DesignationDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<DesignationDto>(new CommandDefinition(
            "hrms.usp_Designations_GetAll",
            new { TenantId = tenantId, CompanyId = companyId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<DesignationDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid designationId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DesignationDto>(new CommandDefinition(
            "hrms.usp_Designations_GetById",
            new { TenantId = tenantId, CompanyId = companyId, DesignationId = designationId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<DesignationDto?> GetByCodeAsync(
        Guid tenantId, Guid companyId, string designationCode, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DesignationDto>(new CommandDefinition(
            "hrms.usp_Designations_GetByCode",
            new { TenantId = tenantId, CompanyId = companyId, DesignationCode = designationCode },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<DesignationDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateDesignationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<DesignationDto>(new CommandDefinition(
                "hrms.usp_Designations_Create",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    request.DesignationCode,
                    request.DesignationName,
                    request.Description,
                    request.JobLevel
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                $"Designation code '{request.DesignationCode}' is already in use.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51221)
        {
            throw new InvalidOperationException(
                "The company does not exist, is inactive, or is outside the authenticated tenant.", exception);
        }
    }

    public async Task<DesignationDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid designationId,
        UpdateDesignationRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DesignationDto>(new CommandDefinition(
            "hrms.usp_Designations_Update",
            new
            {
                TenantId = tenantId,
                CompanyId = companyId,
                DesignationId = designationId,
                request.DesignationName,
                request.Description,
                request.JobLevel,
                RowVersion = request.RowVersion
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid designationId,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_Designations_Deactivate",
            new
            {
                TenantId = tenantId,
                CompanyId = companyId,
                DesignationId = designationId,
                RowVersion = rowVersion
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return affected == 1;
    }
}
