using System.Data;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using HRMS.Application.Holidays;
using Microsoft.Data.SqlClient;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Calls tenant-scoped holiday stored procedures.</summary>
internal sealed class HolidayRepository(IDbConnectionFactory connectionFactory) : IHolidayRepository
{
    public async Task<IReadOnlyList<HolidayDto>> GetAllAsync(
        Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<HolidayDto>(new CommandDefinition(
            "hrms.usp_Holidays_GetAll", new { TenantId = tenantId, CompanyId = companyId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<HolidayDto?> GetByIdAsync(
        Guid tenantId, Guid companyId, Guid holidayId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<HolidayDto>(new CommandDefinition(
            "hrms.usp_Holidays_GetById",
            new { TenantId = tenantId, CompanyId = companyId, HolidayId = holidayId },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<HolidayDto?> GetByDateAsync(
        Guid tenantId, Guid companyId, DateTime holidayDate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<HolidayDto>(new CommandDefinition(
            "hrms.usp_Holidays_GetByDate",
            new { TenantId = tenantId, CompanyId = companyId, HolidayDate = holidayDate.Date },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<HolidayDto> CreateAsync(
        Guid tenantId, Guid companyId, CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleAsync<HolidayDto>(new CommandDefinition(
                "hrms.usp_Holidays_Create",
                new
                {
                    TenantId = tenantId, CompanyId = companyId,
                    request.HolidayName, HolidayDate = request.HolidayDate.Date,
                    request.Description, request.IsOptional
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                $"A holiday is already registered for {request.HolidayDate:yyyy-MM-dd}.", exception);
        }
        catch (SqlException exception) when (exception.Number == 51231)
        {
            throw new InvalidOperationException(
                "The company does not exist, is inactive, or is outside the authenticated tenant.", exception);
        }
    }

    public async Task<HolidayDto?> UpdateAsync(
        Guid tenantId, Guid companyId, Guid holidayId, UpdateHolidayRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<HolidayDto>(new CommandDefinition(
                "hrms.usp_Holidays_Update",
                new
                {
                    TenantId = tenantId, CompanyId = companyId, HolidayId = holidayId,
                    request.HolidayName, HolidayDate = request.HolidayDate.Date,
                    request.Description, request.IsOptional, RowVersion = request.RowVersion
                },
                commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new InvalidOperationException(
                $"A holiday is already registered for {request.HolidayDate:yyyy-MM-dd}.", exception);
        }
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId, Guid companyId, Guid holidayId, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "hrms.usp_Holidays_Deactivate",
            new { TenantId = tenantId, CompanyId = companyId, HolidayId = holidayId, RowVersion = rowVersion },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return affected == 1;
    }
}
