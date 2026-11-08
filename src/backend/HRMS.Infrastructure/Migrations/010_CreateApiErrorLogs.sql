/*
    Migration: 010_CreateApiErrorLogs
    Purpose: Extend the existing audit.ErrorLogs table for API diagnostics.
    Existing rows and columns are preserved. HTTP 401 and 404 are excluded.
*/

IF OBJECT_ID(N'audit.ErrorLogs', N'U') IS NULL
    THROW 51010, 'Required table audit.ErrorLogs does not exist.', 1;

IF COL_LENGTH(N'audit.ErrorLogs', N'TraceId') IS NULL
    ALTER TABLE [audit].[ErrorLogs] ADD [TraceId] nvarchar(100) NULL;

IF COL_LENGTH(N'audit.ErrorLogs', N'StatusCode') IS NULL
    ALTER TABLE [audit].[ErrorLogs] ADD [StatusCode] smallint NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'audit.ErrorLogs')
      AND name = N'CK_ErrorLogs_StatusCode'
)
    ALTER TABLE [audit].[ErrorLogs]
        ADD CONSTRAINT [CK_ErrorLogs_StatusCode]
        CHECK
        (
            [StatusCode] IS NULL
            OR
            (
                [StatusCode] >= 400
                AND [StatusCode] <= 599
                AND [StatusCode] NOT IN (401, 404)
            )
        );

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'audit.ErrorLogs')
      AND name = N'IX_ErrorLogs_OccurredAtUtc'
)
    CREATE INDEX [IX_ErrorLogs_OccurredAtUtc]
        ON [audit].[ErrorLogs] ([OccurredAtUtc] DESC)
        INCLUDE ([StatusCode], [HttpMethod], [RequestPath], [TraceId]);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'audit.ErrorLogs')
      AND name = N'IX_ErrorLogs_Tenant_OccurredAtUtc'
)
    CREATE INDEX [IX_ErrorLogs_Tenant_OccurredAtUtc]
        ON [audit].[ErrorLogs] ([TenantId], [OccurredAtUtc] DESC)
        INCLUDE ([StatusCode], [UserId], [TraceId])
        WHERE [TenantId] IS NOT NULL;
