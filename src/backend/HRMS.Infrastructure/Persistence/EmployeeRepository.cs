using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Employees;

namespace HRMS.Infrastructure.Persistence;

internal sealed class EmployeeRepository(IDbConnectionFactory connectionFactory) : IEmployeeRepository
{
    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(Guid tenantId, Guid companyId, string? search, string? status, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<EmployeeDto>(new CommandDefinition("hrms.usp_Employees_GetAll",
            new { TenantId = tenantId, CompanyId = companyId, Search = search, EmploymentStatus = status },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid tenantId, Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EmployeeDto>(new CommandDefinition("hrms.usp_Employees_GetById",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<EmployeeDto> CreateAsync(Guid tenantId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<EmployeeDto>(new CommandDefinition("hrms.usp_Employees_Create",
            new { TenantId = tenantId, CompanyId = companyId, request.EmployeeCode, request.FirstName, request.MiddleName, request.LastName, request.PreferredName, request.WorkEmail, request.HireDate },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<EmployeeDto?> UpdateAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EmployeeDto>(new CommandDefinition("hrms.usp_Employees_Update",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId, request.FirstName, request.MiddleName, request.LastName, request.PreferredName, request.WorkEmail, request.HireDate, request.RowVersion },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateStatusAsync(Guid tenantId, Guid companyId, Guid employeeId, UpdateEmployeeStatusRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition("hrms.usp_Employees_UpdateStatus",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId, request.EmploymentStatus, request.TerminationDate, request.RowVersion },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return affected == 1;
    }
}
