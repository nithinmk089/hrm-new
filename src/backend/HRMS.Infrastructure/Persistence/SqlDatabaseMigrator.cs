using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using HRMS.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Persistence;

internal sealed class SqlDatabaseMigrator(
    IDbConnectionFactory connectionFactory,
    ILogger<SqlDatabaseMigrator> logger) : IDatabaseMigrator
{
    private const string HistoryTable = "[dbo].[__HrmsSchemaMigrations]";
    private const string ApplicationLockResource = "HRMS.Database.Migrations";

    private static readonly Regex ValidMigrationFileName = new(
        @"^\d{3,}_[A-Za-z0-9][A-Za-z0-9_-]*\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GoBatchSeparator = new(
        @"^\s*GO\s*(?:--.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var migrationDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");

        if (!Directory.Exists(migrationDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The database migration directory was not found: '{migrationDirectory}'.");
        }

        var scripts = await LoadMigrationScriptsAsync(migrationDirectory, cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var lockResult = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    """
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock
                        @Resource = @Resource,
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 60000;
                    SELECT @result;
                    """,
                    new { Resource = ApplicationLockResource },
                    transaction,
                    cancellationToken: cancellationToken));

            if (lockResult < 0)
            {
                throw new InvalidOperationException(
                    $"Could not acquire the database migration lock. SQL Server returned {lockResult}.");
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    IF OBJECT_ID(N'dbo.__HrmsSchemaMigrations', N'U') IS NULL
                    BEGIN
                        CREATE TABLE {HistoryTable}
                        (
                            [MigrationId] nvarchar(200) NOT NULL,
                            [Checksum] char(64) NOT NULL,
                            [AppliedAtUtc] datetime2(7) NOT NULL
                                CONSTRAINT [DF_HrmsSchemaMigrations_AppliedAtUtc]
                                DEFAULT SYSUTCDATETIME(),
                            [ExecutionDurationMs] bigint NOT NULL,
                            CONSTRAINT [PK_HrmsSchemaMigrations]
                                PRIMARY KEY ([MigrationId])
                        );
                    END;
                    """,
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            var appliedMigrations = (await connection.QueryAsync<AppliedMigration>(
                new CommandDefinition(
                    $"SELECT [MigrationId], [Checksum] FROM {HistoryTable};",
                    transaction: transaction,
                    cancellationToken: cancellationToken)))
                .ToDictionary(item => item.MigrationId, StringComparer.OrdinalIgnoreCase);

            var currentMigrationIds = scripts
                .Select(script => script.MigrationId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingScripts = appliedMigrations.Keys
                .Where(migrationId => !currentMigrationIds.Contains(migrationId))
                .ToArray();

            if (missingScripts.Length > 0)
            {
                throw new InvalidOperationException(
                    "Previously applied migration script(s) are missing from the repository: " +
                    string.Join(", ", missingScripts));
            }

            var pendingScripts = new List<MigrationScript>();

            foreach (var script in scripts)
            {
                if (appliedMigrations.TryGetValue(script.MigrationId, out var applied))
                {
                    if (!string.Equals(applied.Checksum, script.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Migration '{script.MigrationId}' has changed after it was applied. " +
                            "Create a new migration instead of editing an applied migration.");
                    }

                    continue;
                }

                pendingScripts.Add(script);
            }

            foreach (var script in pendingScripts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                logger.LogInformation("Applying database migration {MigrationId}.", script.MigrationId);

                foreach (var batch in GoBatchSeparator.Split(script.Content))
                {
                    if (string.IsNullOrWhiteSpace(batch))
                    {
                        continue;
                    }

                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            batch,
                            transaction: transaction,
                            commandTimeout: 120,
                            cancellationToken: cancellationToken));
                }

                stopwatch.Stop();

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        $"""
                        INSERT INTO {HistoryTable}
                            ([MigrationId], [Checksum], [ExecutionDurationMs])
                        VALUES
                            (@MigrationId, @Checksum, @ExecutionDurationMs);
                        """,
                        new
                        {
                            script.MigrationId,
                            script.Checksum,
                            ExecutionDurationMs = stopwatch.ElapsedMilliseconds
                        },
                        transaction,
                        cancellationToken: cancellationToken));

                logger.LogInformation(
                    "Applied database migration {MigrationId} in {ElapsedMilliseconds} ms.",
                    script.MigrationId,
                    stopwatch.ElapsedMilliseconds);
            }

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Database migration completed. {AppliedCount} migration(s) applied.",
                pendingScripts.Count);

            return pendingScripts.Count;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<IReadOnlyList<MigrationScript>> LoadMigrationScriptsAsync(
        string migrationDirectory,
        CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(migrationDirectory, "*.sql")
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scripts = new List<MigrationScript>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);

            if (!ValidMigrationFileName.IsMatch(fileName))
            {
                throw new InvalidOperationException(
                    $"Invalid migration file name '{fileName}'. Expected format: 001_Description.sql.");
            }

            var migrationId = Path.GetFileNameWithoutExtension(file);

            if (migrationId.Length > 200)
            {
                throw new InvalidOperationException(
                    $"Migration ID '{migrationId}' exceeds the 200-character limit.");
            }

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var normalizedContent = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var checksum = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedContent)));

            scripts.Add(new MigrationScript(migrationId, content, checksum));
        }

        return scripts;
    }

    private sealed record MigrationScript(string MigrationId, string Content, string Checksum);

    private sealed record AppliedMigration(string MigrationId, string Checksum);
}
