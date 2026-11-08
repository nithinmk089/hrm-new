using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.LeaveBalances;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class EmployeeLeaveBalanceRepository(IDbConnectionFactory connectionFactory) : IEmployeeLeaveBalanceRepository
{
    public async Task<IReadOnlyList<EmployeeLeaveBalanceDto>> GetAllAsync(
        Guid tenantId, Guid companyId, Guid employeeId, short leaveYear, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<EmployeeLeaveBalanceDto>(new CommandDefinition(
            "hrms.usp_EmployeeLeaveBalances_GetAll",
            new { TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId, LeaveYear = leaveYear },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<EmployeeLeaveBalanceDto> ApplyTransactionAsync(
        Guid tenantId, Guid companyId, Guid employeeId, Guid actorUserId,
        ApplyLeaveBalanceTransactionRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<EmployeeLeaveBalanceDto>(new CommandDefinition(
                "hrms.usp_EmployeeLeaveBalances_ApplyTransaction",
                new
                {
                    TenantId = tenantId, CompanyId = companyId, EmployeeId = employeeId,
                    request.LeaveTypeId, request.LeaveYear, request.TransactionType,
                    request.DaysDelta, request.Notes, ActorUserId = actorUserId
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is >= 51270 and <= 51273)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("A concurrent balance initialization was detected. Reload the balance and retry.", exception);
        }
    }
}
