using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.LeaveRequests;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class LeaveRequestLifecycleRepository(IDbConnectionFactory connectionFactory)
    : ILeaveRequestLifecycleRepository
{
    public async Task<IReadOnlyList<EmployeeLeaveRequestItem>> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<EmployeeLeaveRequestItem>(new CommandDefinition(
            "hrms.usp_LeaveRequests_GetMine",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LeaveRequestHistoryItem>> GetHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LeaveRequestHistoryItem>(new CommandDefinition(
            "hrms.usp_LeaveRequests_GetMyHistory",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId, LeaveRequestId = leaveRequestId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<LeaveRequestCancellationResult> CancelAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        byte[] rowVersion, string? comments, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<LeaveRequestCancellationResult>(new CommandDefinition(
                "hrms.usp_LeaveRequests_Cancel",
                new
                {
                    TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                    LeaveRequestId = leaveRequestId, RowVersion = rowVersion, Comments = comments
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is >= 51300 and <= 51309)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }
}
