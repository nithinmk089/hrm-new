/*
    Migration: 010_CreateApiErrorLogs
    Purpose: Persist failed API responses for operational diagnostics.
    Exclusions: HTTP 401 and 404 responses are intentionally not recorded.
*/

IF SCHEMA_ID(N'audit') IS NULL
    THROW 51010, 'Required schema audit does not exist.', 1;

CREATE TABLE [audit].[ErrorLogs]
(
    [ErrorLogId] bigint IDENTITY(1,1) NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NULL,
    [TraceId] nvarchar(100) NOT NULL,
    [HttpMethod] varchar(10) NOT NULL,
    [RequestPath] nvarchar(2048) NOT NULL,
    [StatusCode] smallint NOT NULL,
    [ErrorType] nvarchar(500) NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [StackTrace] nvarchar(max) NULL,
    [OccurredAtUtc] datetime2(7) NOT NULL
        CONSTRAINT [DF_ErrorLogs_OccurredAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_ErrorLogs] PRIMARY KEY CLUSTERED ([ErrorLogId]),
    CONSTRAINT [CK_ErrorLogs_StatusCode]
        CHECK ([StatusCode] >= 400 AND [StatusCode] <= 599
            AND [StatusCode] NOT IN (401, 404))
);
GO

CREATE INDEX [IX_ErrorLogs_OccurredAtUtc]
    ON [audit].[ErrorLogs] ([OccurredAtUtc] DESC)
    INCLUDE ([StatusCode], [HttpMethod], [RequestPath], [TraceId]);
GO

CREATE INDEX [IX_ErrorLogs_Tenant_OccurredAtUtc]
    ON [audit].[ErrorLogs] ([TenantId], [OccurredAtUtc] DESC)
    INCLUDE ([StatusCode], [UserId], [TraceId])
    WHERE [TenantId] IS NOT NULL;
GO
