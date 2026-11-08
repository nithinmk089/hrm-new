using System.Data;
using System.Text.Json;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Timesheets;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class TimesheetRepository(IDbConnectionFactory connectionFactory) : ITimesheetRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TimesheetDetailDto?> GetMineAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "hrms.usp_Timesheets_GetMine",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId, WeekStartDate = weekStartDate },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var header = await grid.ReadSingleOrDefaultAsync<TimesheetHeaderRow>();
        var entries = (await grid.ReadAsync<TimesheetEntryDto>()).AsList();
        return header is null
            ? null
            : new TimesheetDetailDto(
                header.TimesheetId, header.WeekStartDate, header.WeekEndDate, header.Status,
                header.SubmittedAtUtc, header.ReviewedByEmployeeId, header.ReviewedAtUtc,
                header.ReviewComments, header.RowVersion, header.TotalHours, entries);
    }

    public async Task<TimesheetHistoryResponse> GetMineHistoryAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "hrms.usp_Timesheets_GetMineHistory",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        var timesheets = (await grid.ReadAsync<TimesheetSummaryDto>()).AsList();
        var history = (await grid.ReadAsync<TimesheetHistoryItem>()).AsList();
        return new TimesheetHistoryResponse(timesheets, history);
    }

    public async Task<TimesheetCreateResult> CreateAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, DateOnly weekStartDate,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await ExecuteAsync(() => connection.QuerySingleAsync<TimesheetCreateResult>(new CommandDefinition(
            "hrms.usp_Timesheets_Create",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId, WeekStartDate = weekStartDate },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    public async Task<SaveTimesheetEntriesResult> SaveEntriesAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        SaveTimesheetEntriesRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var entriesJson = JsonSerializer.Serialize(request.Entries, JsonOptions);
        return await ExecuteAsync(() => connection.QuerySingleAsync<SaveTimesheetEntriesResult>(new CommandDefinition(
            "hrms.usp_Timesheets_SaveEntries",
            new
            {
                TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                TimesheetId = timesheetId, RowVersion = request.RowVersion, EntriesJson = entriesJson
            },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    public async Task<TimesheetSubmitResult> SubmitAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await ExecuteAsync(() => connection.QuerySingleAsync<TimesheetSubmitResult>(new CommandDefinition(
            "hrms.usp_Timesheets_Submit",
            new
            {
                TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                TimesheetId = timesheetId, RowVersion = rowVersion
            },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    public async Task<IReadOnlyList<TimesheetApprovalQueueItem>> GetPendingApprovalsAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await ExecuteAsync(() => connection.QueryAsync<TimesheetApprovalQueueItem>(new CommandDefinition(
            "hrms.usp_Timesheets_GetPendingApprovals",
            new { TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
        return rows.AsList();
    }

    public async Task<TimesheetDecisionResult> DecideAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid timesheetId,
        string decision, TimesheetDecisionRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await ExecuteAsync(() => connection.QuerySingleAsync<TimesheetDecisionResult>(new CommandDefinition(
            "hrms.usp_Timesheets_Decide",
            new
            {
                TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                TimesheetId = timesheetId, Decision = decision,
                RowVersion = request.RowVersion, Comments = request.Comments
            },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (SqlException exception) when (exception.Number is >= 51400 and <= 51423)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    private sealed record TimesheetHeaderRow(
        Guid TimesheetId, DateOnly WeekStartDate, DateOnly WeekEndDate, string Status,
        DateTime? SubmittedAtUtc, Guid? ReviewedByEmployeeId, DateTime? ReviewedAtUtc,
        string? ReviewComments, byte[] RowVersion, decimal TotalHours);
}
