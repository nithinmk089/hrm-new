namespace HRMS.Application.Abstractions.Persistence;

/// <summary>
/// Applies pending, version-controlled database migrations.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Applies all pending migrations and returns the number of migrations applied.
    /// </summary>
    Task<int> MigrateAsync(CancellationToken cancellationToken = default);
}
