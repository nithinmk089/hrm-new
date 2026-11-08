using System.Data;
using System.Text.Json;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.LeaveRequests;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

internal sealed class LeaveRequestRepository(IDbConnectionFactory connectionFactory) : ILeaveRequestRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<LeaveRequestCalculationContext> GetCalculationContextAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, Guid leaveTypeId,
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "hrms.usp_LeaveRequests_GetSubmissionContext",
            new
            {
                TenantId = tenantId, CompanyId = companyId, ActorUserId = actorUserId,
                LeaveTypeId = leaveTypeId, FromDate = fromDate, ToDate = toDate
            },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var header = await grid.ReadSingleOrDefaultAsync<SubmissionContextHeader>()
            ?? throw new InvalidOperationException(
                "The authenticated user must have an active employee mapping and the leave type must be active in this company.");
        var companyWeek = (await grid.ReadAsync<LeaveWorkingWeekDay>()).AsList();
        var assignments = (await grid.ReadAsync<LeaveOfficeAssignment>()).AsList();
        var officeRows = (await grid.ReadAsync<OfficeWorkingWeekRow>()).AsList();
        var holidays = (await grid.ReadAsync<LeaveCalendarHoliday>()).AsList();

        var officeWeeks = officeRows
            .GroupBy(row => row.OfficeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LeaveWorkingWeekDay>)group
                    .Select(row => new LeaveWorkingWeekDay(row.DayOfWeek, row.IsWorkingDay))
                    .ToArray());

        return new LeaveRequestCalculationContext(
            header.EmployeeId,
            header.LeaveTypeId,
            header.LeaveTypeCode,
            header.LeaveTypeName,
            header.RequiresApproval,
            header.AllowHalfDay,
            companyWeek,
            officeWeeks,
            assignments,
            holidays);
    }

    public async Task<LeaveRequestSubmissionResult> SubmitCalculatedAsync(
        Guid tenantId, Guid companyId, Guid actorUserId, SubmitLeaveRequest request,
        decimal daysRequested, IReadOnlyList<LeaveYearReservationDto> yearAllocations,
        IReadOnlyList<DateOnly> excludedDates, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var allocationsJson = JsonSerializer.Serialize(yearAllocations, JsonOptions);

        try
        {
            using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
                "hrms.usp_LeaveRequests_Submit",
                new
                {
                    TenantId = tenantId,
                    CompanyId = companyId,
                    ActorUserId = actorUserId,
                    request.LeaveTypeId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    request.IsHalfDay,
                    request.HalfDayPeriod,
                    DaysRequested = daysRequested,
                    request.Reason,
                    YearDaysJson = allocationsJson
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

            var leaveRequest = await grid.ReadSingleAsync<LeaveRequestDto>();
            var persistedAllocations = (await grid.ReadAsync<LeaveYearReservationDto>()).AsList();
            return new LeaveRequestSubmissionResult(leaveRequest, persistedAllocations, excludedDates);
        }
        catch (SqlException exception) when (exception.Number is >= 51280 and <= 51288)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                "The leave request conflicts with another request submitted at the same time. Reload and retry.", exception);
        }
    }

    private sealed record SubmissionContextHeader(
        Guid EmployeeId,
        Guid LeaveTypeId,
        string LeaveTypeCode,
        string LeaveTypeName,
        bool RequiresApproval,
        bool AllowHalfDay);

    private sealed record OfficeWorkingWeekRow(Guid OfficeId, byte DayOfWeek, bool IsWorkingDay);
}
