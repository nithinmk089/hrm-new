using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.LeaveRequests;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class LeaveApprovalRepository(IDbConnectionFactory connectionFactory) : ILeaveApprovalRepository
{
    public async Task<IReadOnlyList<LeaveApprovalQueueItem>> GetPendingAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LeaveApprovalQueueItem>(new CommandDefinition(
            "hrms.usp_LeaveRequests_GetPendingApprovals",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<LeaveApprovalDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveRequestId,
        string action, byte[] rowVersion, string? comments, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<LeaveApprovalDecisionResult>(new CommandDefinition(
                "hrms.usp_LeaveRequests_Decide",
                new
                {
                    TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                    LeaveRequestId = leaveRequestId, Action = action,
                    RowVersion = rowVersion, Comments = comments
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is >= 51290 and <= 51299)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }
}
