# SQL Server database migrations

SQL migration scripts are stored in this directory and are applied by the
`IDatabaseMigrator` implementation in the Infrastructure project.

## File naming

Use a zero-padded, increasing numeric prefix followed by a short description:

- `001_CreateHrmsSchema.sql`
- `002_CreateEmployeeTables.sql`
- `003_AddEmployeeIndexes.sql`

Once a migration has been applied to any shared environment, do not edit or
delete it. Add a new migration to change the schema.

## Execution

From the repository root, run:

```powershell
dotnet run --project .\src\backend\HRMS.API\HRMS.API.csproj -- --migrate
```

The migration command requires the `ConnectionStrings:Hrms` setting. For local
development, configure it with .NET User Secrets rather than committing
credentials:

```powershell
dotnet user-secrets set "ConnectionStrings:Hrms" "<your SQL Server connection string>" --project .\src\backend\HRMS.API\HRMS.API.csproj
```

Normal API startup does not apply migrations. Run the migration command
explicitly before starting the API after a schema change.

## Safety behavior

- Scripts are applied in filename order.
- The runner creates `dbo.__HrmsSchemaMigrations` to record applied scripts.
- SHA-256 checksums detect changes to scripts that have already been applied.
- A SQL Server application lock prevents concurrent migration processes.
- Pending scripts and their history records are committed in one transaction.
- SQLCMD-style `GO` batch separators are supported when `GO` appears alone on
  a line (an optional trailing SQL comment is allowed).
- A migration failure rolls back the pending migration transaction.
