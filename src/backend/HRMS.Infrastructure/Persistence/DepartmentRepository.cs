using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Departments;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Calls tenant-scoped department stored procedures.</summary>
internal sealed class DepartmentRepository(IDbConnectionFactory connectionFactory) : IDepartmentRepository
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<DepartmentDto>(new CommandDefinition(
            "hrms.usp_Departments_GetAll",
            new { TenantId = tenantId, CompanyId = companyId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<DepartmentDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid departmentId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DepartmentDto>(new CommandDefinition(
            "hrms.usp_Departments_GetById",
            new { TenantId = tenantId, CompanyId = companyId, DepartmentId = departmentId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<DepartmentDto?> GetByCodeAsync(
        Guid tenantId, Guid companyId, string departmentCode, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DepartmentDto>(new CommandDefinition(
            "hrms.usp_Departments_GetByCode",
            new { TenantId = tenantId, CompanyId = companyId, DepartmentCode = departmentCode },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ParentExistsAsync(
        Guid tenantId, Guid companyId, Guid parentDepartmentId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "hrms.usp_Departments_ParentExists",
            new { TenantId = tenantId, CompanyId = companyId, ParentDepartmentId = parentDepartmentId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<DepartmentDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<DepartmentDto>(new CommandDefinition(
                "hrms.usp_Departments_Create",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    request.DepartmentCode,
                    request.DepartmentName,
                    request.Description,
                    request.ParentDepartmentId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                $"Department code '{request.DepartmentCode}' is already in use.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51201)
        {
            throw new InvalidOperationException(
                "The company does not exist, is inactive, or is outside the authenticated tenant.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51202)
        {
            throw new ArgumentException(
                "ParentDepartmentId must identify an active department in the same company.",
                nameof(request.ParentDepartmentId), exception);
        }
    }

    public async Task<DepartmentDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid departmentId,
        UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<DepartmentDto>(new CommandDefinition(
                "hrms.usp_Departments_Update",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    DepartmentId = departmentId,
                    request.DepartmentName,
                    request.Description,
                    request.ParentDepartmentId,
                    RowVersion = request.RowVersion
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 51202 or 51203 or 51204)
        {
            throw new ArgumentException(
                exception.Message, nameof(request.ParentDepartmentId), exception);
        }
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid departmentId,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_Departments_Deactivate",
            new
            {
                TenantId = tenantId,
                CompanyId = companyId,
                DepartmentId = departmentId,
                RowVersion = rowVersion
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return affected == 1;
    }
}
