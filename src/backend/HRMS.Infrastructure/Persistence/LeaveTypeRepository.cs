using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.LeaveTypes;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Calls tenant-scoped leave-type stored procedures.</summary>
internal sealed class LeaveTypeRepository(IDbConnectionFactory connectionFactory) : ILeaveTypeRepository
{
    public async Task<IReadOnlyList<LeaveTypeDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LeaveTypeDto>(new CommandDefinition(
            "hrms.usp_LeaveTypes_GetAll", new { TenantId = tenantId, CompanyId = companyId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<LeaveTypeDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<LeaveTypeDto>(new CommandDefinition(
            "hrms.usp_LeaveTypes_GetById",
            new { TenantId = tenantId, CompanyId = companyId, LeaveTypeId = leaveTypeId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<LeaveTypeDto?> GetByCodeAsync(
        Guid tenantId, Guid companyId, string leaveTypeCode, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<LeaveTypeDto>(new CommandDefinition(
            "hrms.usp_LeaveTypes_GetByCode",
            new { TenantId = tenantId, CompanyId = companyId, LeaveTypeCode = leaveTypeCode },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<LeaveTypeDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<LeaveTypeDto>(new CommandDefinition(
                "hrms.usp_LeaveTypes_Create",
                new
                {
                    TenantId = tenantId, CompanyId = companyId,
                    request.LeaveTypeCode, request.LeaveTypeName, request.Description,
                    request.IsPaid, request.RequiresApproval, request.AnnualEntitlementDays,
                    request.AllowHalfDay, request.AllowCarryForward, request.MaxCarryForwardDays
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                $"Leave type code '{request.LeaveTypeCode}' is already in use.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51241)
        {
            throw new InvalidOperationException(
                "The company does not exist, is inactive, or is outside the authenticated tenant.", exception);
        }
    }

    public async Task<LeaveTypeDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<LeaveTypeDto>(new CommandDefinition(
            "hrms.usp_LeaveTypes_Update",
            new
            {
                TenantId = tenantId, CompanyId = companyId, LeaveTypeId = leaveTypeId,
                request.LeaveTypeName, request.Description, request.IsPaid, request.RequiresApproval,
                request.AnnualEntitlementDays, request.AllowHalfDay, request.AllowCarryForward,
                request.MaxCarryForwardDays, RowVersion = request.RowVersion
            },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid leaveTypeId, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_LeaveTypes_Deactivate",
            new { TenantId = tenantId, CompanyId = companyId, LeaveTypeId = leaveTypeId, RowVersion = rowVersion },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return affected == 1;
    }
}
