/*
    Migration: 004_CreateIdentityAndSecurityTables
    Purpose:
      - Create global user identities and company memberships.
      - Create company-scoped roles and permission assignments.
      - Store hashed refresh tokens for rotation and revocation.
      - Record security-relevant audit events.
*/

CREATE TABLE [hrms].[Users]
(
    [UserId] uniqueidentifier NOT NULL CONSTRAINT [DF_Users_UserId] DEFAULT NEWSEQUENTIALID(),
    [Email] nvarchar(254) NOT NULL,
    [NormalizedEmail] nvarchar(254) NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [DisplayName] nvarchar(150) NOT NULL,
    [PhoneNumber] nvarchar(30) NULL,
    [EmailConfirmedAtUtc] datetime2(7) NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (1),
    [FailedLoginCount] int NOT NULL CONSTRAINT [DF_Users_FailedLoginCount] DEFAULT (0),
    [LockoutEndUtc] datetime2(7) NULL,
    [LastLoginAtUtc] datetime2(7) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_Users_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedAtUtc] datetime2(7) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId]),
    CONSTRAINT [CK_Users_Email_NotBlank] CHECK (LEN(LTRIM(RTRIM([Email]))) > 0),
    CONSTRAINT [CK_Users_NormalizedEmail_NotBlank] CHECK (LEN(LTRIM(RTRIM([NormalizedEmail]))) > 0),
    CONSTRAINT [CK_Users_PasswordHash_NotBlank] CHECK (LEN(LTRIM(RTRIM([PasswordHash]))) > 0),
    CONSTRAINT [CK_Users_DisplayName_NotBlank] CHECK (LEN(LTRIM(RTRIM([DisplayName]))) > 0),
    CONSTRAINT [CK_Users_FailedLoginCount_NonNegative] CHECK ([FailedLoginCount] >= 0)
);
GO

CREATE UNIQUE INDEX [UX_Users_NormalizedEmail]
    ON [hrms].[Users] ([NormalizedEmail]);
GO

CREATE TABLE [hrms].[CompanyUserMemberships]
(
    [MembershipId] uniqueidentifier NOT NULL CONSTRAINT [DF_CompanyUserMemberships_MembershipId] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [MembershipStatus] varchar(20) NOT NULL CONSTRAINT [DF_CompanyUserMemberships_Status] DEFAULT ('Invited'),
    [IsDefault] bit NOT NULL CONSTRAINT [DF_CompanyUserMemberships_IsDefault] DEFAULT (0),
    [JoinedAtUtc] datetime2(7) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_CompanyUserMemberships_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CompanyUserMemberships] PRIMARY KEY CLUSTERED ([MembershipId]),
    CONSTRAINT [UQ_CompanyUserMemberships_Company_Membership] UNIQUE ([CompanyId], [MembershipId]),
    CONSTRAINT [UQ_CompanyUserMemberships_Membership_User] UNIQUE ([MembershipId], [UserId]),
    CONSTRAINT [UQ_CompanyUserMemberships_Company_User] UNIQUE ([CompanyId], [UserId]),
    CONSTRAINT [FK_CompanyUserMemberships_Company] FOREIGN KEY ([CompanyId]) REFERENCES [hrms].[Companies] ([CompanyId]),
    CONSTRAINT [FK_CompanyUserMemberships_User] FOREIGN KEY ([UserId]) REFERENCES [hrms].[Users] ([UserId]),
    CONSTRAINT [CK_CompanyUserMemberships_Status] CHECK ([MembershipStatus] IN ('Invited', 'Active', 'Suspended', 'Removed'))
);
GO

CREATE UNIQUE INDEX [UX_CompanyUserMemberships_OneDefaultPerUser]
    ON [hrms].[CompanyUserMemberships] ([UserId])
    WHERE [IsDefault] = 1 AND [MembershipStatus] = 'Active';
GO

CREATE INDEX [IX_CompanyUserMemberships_User_Status]
    ON [hrms].[CompanyUserMemberships] ([UserId], [MembershipStatus])
    INCLUDE ([CompanyId], [IsDefault]);
GO

CREATE TABLE [hrms].[Roles]
(
    [RoleId] uniqueidentifier NOT NULL CONSTRAINT [DF_Roles_RoleId] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [RoleCode] nvarchar(50) NOT NULL,
    [RoleName] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsSystemRole] bit NOT NULL CONSTRAINT [DF_Roles_IsSystemRole] DEFAULT (0),
    [IsActive] bit NOT NULL CONSTRAINT [DF_Roles_IsActive] DEFAULT (1),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_Roles_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedAtUtc] datetime2(7) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId]),
    CONSTRAINT [UQ_Roles_Company_Role] UNIQUE ([CompanyId], [RoleId]),
    CONSTRAINT [FK_Roles_Company] FOREIGN KEY ([CompanyId]) REFERENCES [hrms].[Companies] ([CompanyId]),
    CONSTRAINT [CK_Roles_RoleCode_NotBlank] CHECK (LEN(LTRIM(RTRIM([RoleCode]))) > 0),
    CONSTRAINT [CK_Roles_RoleName_NotBlank] CHECK (LEN(LTRIM(RTRIM([RoleName]))) > 0)
);
GO

CREATE UNIQUE INDEX [UX_Roles_Company_Code]
    ON [hrms].[Roles] ([CompanyId], [RoleCode]);
GO

CREATE TABLE [hrms].[Permissions]
(
    [PermissionId] uniqueidentifier NOT NULL CONSTRAINT [DF_Permissions_PermissionId] DEFAULT NEWSEQUENTIALID(),
    [PermissionCode] nvarchar(100) NOT NULL,
    [PermissionName] nvarchar(150) NOT NULL,
    [ModuleCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_Permissions_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([PermissionId]),
    CONSTRAINT [CK_Permissions_Code_NotBlank] CHECK (LEN(LTRIM(RTRIM([PermissionCode]))) > 0),
    CONSTRAINT [CK_Permissions_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([PermissionName]))) > 0),
    CONSTRAINT [CK_Permissions_Module_NotBlank] CHECK (LEN(LTRIM(RTRIM([ModuleCode]))) > 0)
);
GO

CREATE UNIQUE INDEX [UX_Permissions_Code]
    ON [hrms].[Permissions] ([PermissionCode]);
GO

CREATE TABLE [hrms].[UserRoles]
(
    [CompanyId] uniqueidentifier NOT NULL,
    [MembershipId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [AssignedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_UserRoles_AssignedAtUtc] DEFAULT SYSUTCDATETIME(),
    [AssignedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([CompanyId], [MembershipId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Membership] FOREIGN KEY ([CompanyId], [MembershipId])
        REFERENCES [hrms].[CompanyUserMemberships] ([CompanyId], [MembershipId]),
    CONSTRAINT [FK_UserRoles_Role] FOREIGN KEY ([CompanyId], [RoleId])
        REFERENCES [hrms].[Roles] ([CompanyId], [RoleId])
);
GO

CREATE TABLE [hrms].[RolePermissions]
(
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [GrantedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_RolePermissions_GrantedAtUtc] DEFAULT SYSUTCDATETIME(),
    [GrantedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_Role] FOREIGN KEY ([RoleId]) REFERENCES [hrms].[Roles] ([RoleId]),
    CONSTRAINT [FK_RolePermissions_Permission] FOREIGN KEY ([PermissionId]) REFERENCES [hrms].[Permissions] ([PermissionId])
);
GO

CREATE TABLE [hrms].[RefreshTokens]
(
    [RefreshTokenId] uniqueidentifier NOT NULL CONSTRAINT [DF_RefreshTokens_RefreshTokenId] DEFAULT NEWSEQUENTIALID(),
    [UserId] uniqueidentifier NOT NULL,
    [MembershipId] uniqueidentifier NOT NULL,
    [TokenHash] char(64) NOT NULL,
    [TokenFamilyId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_RefreshTokens_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [ExpiresAtUtc] datetime2(7) NOT NULL,
    [RevokedAtUtc] datetime2(7) NULL,
    [ReplacedByTokenId] uniqueidentifier NULL,
    [CreatedByIp] varchar(45) NULL,
    [RevokedByIp] varchar(45) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([RefreshTokenId]),
    CONSTRAINT [FK_RefreshTokens_User] FOREIGN KEY ([UserId]) REFERENCES [hrms].[Users] ([UserId]),
    CONSTRAINT [FK_RefreshTokens_Membership_User] FOREIGN KEY ([MembershipId], [UserId]) REFERENCES [hrms].[CompanyUserMemberships] ([MembershipId], [UserId]),
    CONSTRAINT [FK_RefreshTokens_ReplacedBy] FOREIGN KEY ([ReplacedByTokenId]) REFERENCES [hrms].[RefreshTokens] ([RefreshTokenId]),
    CONSTRAINT [CK_RefreshTokens_Expiry] CHECK ([ExpiresAtUtc] > [CreatedAtUtc])
);
GO

CREATE UNIQUE INDEX [UX_RefreshTokens_TokenHash]
    ON [hrms].[RefreshTokens] ([TokenHash]);
GO

CREATE INDEX [IX_RefreshTokens_User_Expiry]
    ON [hrms].[RefreshTokens] ([UserId], [ExpiresAtUtc])
    INCLUDE ([RevokedAtUtc], [TokenFamilyId]);
GO

CREATE TABLE [hrms].[AuditLogs]
(
    [AuditLogId] bigint IDENTITY(1,1) NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [ActorUserId] uniqueidentifier NULL,
    [ActionCode] nvarchar(100) NOT NULL,
    [EntityName] nvarchar(150) NOT NULL,
    [EntityId] nvarchar(100) NULL,
    [OccurredAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_AuditLogs_OccurredAtUtc] DEFAULT SYSUTCDATETIME(),
    [IpAddress] varchar(45) NULL,
    [CorrelationId] nvarchar(100) NULL,
    [DetailsJson] nvarchar(max) NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([AuditLogId]),
    CONSTRAINT [FK_AuditLogs_Company] FOREIGN KEY ([CompanyId]) REFERENCES [hrms].[Companies] ([CompanyId]),
    CONSTRAINT [FK_AuditLogs_Actor] FOREIGN KEY ([ActorUserId]) REFERENCES [hrms].[Users] ([UserId]),
    CONSTRAINT [CK_AuditLogs_ActionCode_NotBlank] CHECK (LEN(LTRIM(RTRIM([ActionCode]))) > 0),
    CONSTRAINT [CK_AuditLogs_EntityName_NotBlank] CHECK (LEN(LTRIM(RTRIM([EntityName]))) > 0),
    CONSTRAINT [CK_AuditLogs_DetailsJson] CHECK ([DetailsJson] IS NULL OR ISJSON([DetailsJson]) = 1)
);
GO

CREATE INDEX [IX_AuditLogs_Company_OccurredAt]
    ON [hrms].[AuditLogs] ([CompanyId], [OccurredAtUtc] DESC)
    INCLUDE ([ActorUserId], [ActionCode], [EntityName], [EntityId]);
GO

CREATE INDEX [IX_AuditLogs_Actor_OccurredAt]
    ON [hrms].[AuditLogs] ([ActorUserId], [OccurredAtUtc] DESC);
GO
