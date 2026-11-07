using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace HRMS.DatabaseMigrator;

internal static class Program
{
    private const string MigrationLockName = "HRMS.DatabaseMigrator";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("HRMS_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set HRMS_CONNECTION_STRING to a SQL Server connection string. Do not place credentials in source control.");
            }

            var migrationDirectory = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "database", "migrations"));

            if (!Directory.Exists(migrationDirectory))
            {
                throw new DirectoryNotFoundException($"Migration directory not found: {migrationDirectory}");
            }

            var migrationFiles = Directory.GetFiles(migrationDirectory, "*.sql")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (migrationFiles.Length == 0)
            {
                Console.WriteLine("No SQL migration files found.");
                return 0;
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await EnsureMigrationJournalAsync(connection);
            await AcquireMigrationLockAsync(connection);

            try
            {
                foreach (var migrationFile in migrationFiles)
                {
                    await ApplyMigrationAsync(connection, migrationFile);
                }
            }
            finally
            {
                await ReleaseMigrationLockAsync(connection);
            }

            Console.WriteLine("Database migrations completed successfully.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database migration failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task EnsureMigrationJournalAsync(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'platform') IS NULL
                EXEC(N'CREATE SCHEMA platform');

            IF OBJECT_ID(N'platform.DatabaseVersions', N'U') IS NULL
            BEGIN
                CREATE TABLE platform.DatabaseVersions
                (
                    DatabaseVersionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DatabaseVersions PRIMARY KEY,
                    VersionNumber varchar(50) NOT NULL,
                    Description nvarchar(500) NOT NULL,
                    AppliedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DatabaseVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
                    AppliedBy nvarchar(256) NOT NULL,
                    ScriptChecksum char(64) NULL,
                    CONSTRAINT UQ_DatabaseVersions_VersionNumber UNIQUE (VersionNumber)
                );
            END;
            """;

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AcquireMigrationLockAsync(SqlConnection connection)
    {
        const string sql = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 0;
            SELECT @Result;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Resource", MigrationLockName);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());

        if (result < 0)
        {
            throw new InvalidOperationException(
                "Another HRMS database migration process is running, or the migration lock could not be acquired.");
        }
    }

    private static async Task ReleaseMigrationLockAsync(SqlConnection connection)
    {
        const string sql = """
            EXEC sys.sp_releaseapplock
                @Resource = @Resource,
                @LockOwner = 'Session';
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Resource", MigrationLockName);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ApplyMigrationAsync(SqlConnection connection, string migrationFile)
    {
        var fileName = Path.GetFileName(migrationFile);
        var separatorIndex = fileName.IndexOf('_');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException(
                $"Migration filename '{fileName}' must follow the format NNNN_description.sql.");
        }

        var version = fileName[..separatorIndex];
        if (version.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new InvalidOperationException($"Migration version '{version}' must contain only digits.");
        }

        var script = await File.ReadAllTextAsync(migrationFile, Encoding.UTF8);
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        var existing = await GetAppliedMigrationAsync(connection, version);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(existing.Value.Checksum) &&
                !string.Equals(existing.Value.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Migration {version} has changed since it was applied. Add a new migration instead of editing an applied script.");
            }

            Console.WriteLine($"SKIP {fileName} (already recorded)");
            return;
        }

        Console.WriteLine($"APPLY {fileName}");
        var batches = GoBatchSplitter.Split(script);

        for (var index = 0; index < batches.Count; index++)
        {
            try
            {
                await using var command = new SqlCommand(batches[index], connection)
                {
                    CommandTimeout = 120
                };
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException exception)
            {
                throw new InvalidOperationException(
                    $"Migration {version}, batch {index + 1}/{batches.Count} failed: {exception.Message}",
                    exception);
            }
        }

        await RecordMigrationAsync(connection, version, fileName, checksum);
        Console.WriteLine($"DONE  {fileName}");
    }

    private static async Task<(string Description, string? Checksum)?> GetAppliedMigrationAsync(
        SqlConnection connection,
        string version)
    {
        const string sql = """
            SELECT Description, ScriptChecksum
            FROM platform.DatabaseVersions
            WHERE VersionNumber = @VersionNumber;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@VersionNumber", version);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static async Task RecordMigrationAsync(
        SqlConnection connection,
        string version,
        string fileName,
        string checksum)
    {
        const string sql = """
            IF NOT EXISTS
            (
                SELECT 1
                FROM platform.DatabaseVersions WITH (UPDLOCK, HOLDLOCK)
                WHERE VersionNumber = @VersionNumber
            )
            BEGIN
                INSERT INTO platform.DatabaseVersions
                    (VersionNumber, Description, AppliedBy, ScriptChecksum)
                VALUES
                    (@VersionNumber, @Description, COALESCE(ORIGINAL_LOGIN(), SUSER_SNAME()), @ScriptChecksum);
            END;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@VersionNumber", version);
        command.Parameters.AddWithValue("@Description", $"Applied SQL migration: {fileName}");
        command.Parameters.AddWithValue("@ScriptChecksum", checksum);
        await command.ExecuteNonQueryAsync();
    }
}
