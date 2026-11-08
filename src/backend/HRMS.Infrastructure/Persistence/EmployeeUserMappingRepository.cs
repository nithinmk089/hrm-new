using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.EmployeeUserMappings;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class EmployeeUserMappingRepository(IDbConnectionFactory connectionFactory) : IEmployeeUserMappingRepository
{
    public async Task<EmployeeUserMappingDto?> GetByEmployeeAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EmployeeUserMappingDto>(new CommandDefinition(
            "hrms.usp_EmployeeUserMappings_GetByEmployee",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<EmployeeUserMappingDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeUserMappingRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<EmployeeUserMappingDto>(new CommandDefinition(
                "hrms.usp_EmployeeUserMappings_Create",
                new { TenantId = tenantId, CompanyId = companyId, request.EmployeeId, request.UserId },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("The employee or user already has an active employee mapping.", exception);
        }
        catch (SqlException exception) when (exception.Number is 51261 or 51262)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    public async Task<bool> DeactivateAsync(Guid tenantId, Guid companyId, Guid mappingId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_EmployeeUserMappings_Deactivate",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeUserMappingId = mappingId, RowVersion = rowVersion },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return affected == 1;
    }
}
