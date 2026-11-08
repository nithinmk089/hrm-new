namespace HRMS.Application.Abstractions.Persistence;

/// <summary>Persists API failures for operational diagnostics.</summary>
public interface IApiErrorLogRepository
{
    Task InsertAsync(ApiErrorLogEntry entry, CancellationToken cancellationToken = default);
}
