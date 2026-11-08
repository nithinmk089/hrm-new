using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.WorkingWeeks;

namespace HRMS.Infrastructure.Persistence;

internal sealed class WorkingWeekRepository(IDbConnectionFactory connectionFactory) : IWorkingWeekRepository
{
    public async Task<WorkingWeekDto> GetAsync(
        Guid tenantId, Guid companyId, Guid? officeId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "hrms.usp_WorkingWeek_Get",
            new { TenantId = tenantId, CompanyId = companyId, OfficeId = officeId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        var header = await grid.ReadSingleAsync<WorkingWeekHeader>();
        var days = (await grid.ReadAsync<WorkingWeekDayDto>()).AsList();
        return new WorkingWeekDto(companyId, officeId, header.IsConfigured, header.IsInherited, days);
    }

    public async Task<WorkingWeekDto> SaveAsync(
        Guid tenantId, Guid companyId, Guid? officeId,
        SaveWorkingWeekRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("CompanyId", companyId);
        parameters.Add("OfficeId", officeId);
        foreach (var day in request.Days)
            parameters.Add($"Day{day.DayOfWeek}", day.IsWorkingDay);

        await connection.ExecuteAsync(new CommandDefinition(
            "hrms.usp_WorkingWeek_Save", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return await GetAsync(tenantId, companyId, officeId, cancellationToken);
    }

    private sealed record WorkingWeekHeader(bool IsConfigured, bool IsInherited);
}
