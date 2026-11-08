/*
    Migration: 003_CreateEmployeeManagementTables
    Purpose:
      - Create employee identity records.
      - Preserve employment assignment history.
      - Preserve reporting-manager history.
      - Store employee contact/address details.
      - Store document metadata while keeping file bytes in external storage.
*/

CREATE TABLE [hrms].[Employees]
(
    [EmployeeId] uniqueidentifier NOT NULL CONSTRAINT [DF_Employees_EmployeeId] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeCode] nvarchar(30) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [MiddleName] nvarchar(100) NULL,
    [LastName] nvarchar(100) NOT NULL,
    [PreferredName] nvarchar(100) NULL,
    [WorkEmail] nvarchar(254) NULL,
    [HireDate] date NOT NULL,
    [EmploymentStatus] varchar(20) NOT NULL CONSTRAINT [DF_Employees_EmploymentStatus] DEFAULT ('Preboarding'),
    [TerminationDate] date NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Employees_IsActive] DEFAULT (1),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_Employees_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY CLUSTERED ([EmployeeId]),
    CONSTRAINT [UQ_Employees_Company_Employee] UNIQUE ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_Employees_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [hrms].[Companies] ([CompanyId]),
    CONSTRAINT [CK_Employees_EmployeeCode_NotBlank] CHECK (LEN(LTRIM(RTRIM([EmployeeCode]))) > 0),
    CONSTRAINT [CK_Employees_FirstName_NotBlank] CHECK (LEN(LTRIM(RTRIM([FirstName]))) > 0),
    CONSTRAINT [CK_Employees_LastName_NotBlank] CHECK (LEN(LTRIM(RTRIM([LastName]))) > 0),
    CONSTRAINT [CK_Employees_EmploymentStatus] CHECK ([EmploymentStatus] IN ('Preboarding', 'Active', 'OnLeave', 'Inactive', 'Terminated')),
    CONSTRAINT [CK_Employees_TerminationDate] CHECK ([TerminationDate] IS NULL OR [TerminationDate] >= [HireDate]),
    CONSTRAINT [CK_Employees_TerminatedStatus] CHECK ([EmploymentStatus] <> 'Terminated' OR [TerminationDate] IS NOT NULL)
);
GO

CREATE UNIQUE INDEX [UX_Employees_Company_Code_Active]
    ON [hrms].[Employees] ([CompanyId], [EmployeeCode])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE UNIQUE INDEX [UX_Employees_Company_WorkEmail_Active]
    ON [hrms].[Employees] ([CompanyId], [WorkEmail])
    WHERE [WorkEmail] IS NOT NULL AND [DeletedAtUtc] IS NULL;
GO

CREATE INDEX [IX_Employees_Company_Name_Active]
    ON [hrms].[Employees] ([CompanyId], [LastName], [FirstName])
    INCLUDE ([EmployeeCode], [WorkEmail], [EmploymentStatus], [HireDate])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[EmployeeEmployment]
(
    [EmployeeEmploymentId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeEmployment_Id] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [OfficeId] uniqueidentifier NULL,
    [DesignationId] uniqueidentifier NULL,
    [EmploymentType] varchar(20) NOT NULL,
    [EmploymentStatus] varchar(20) NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [IsPrimary] bit NOT NULL CONSTRAINT [DF_EmployeeEmployment_IsPrimary] DEFAULT (1),
    [Notes] nvarchar(1000) NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeEmployment_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeEmployment] PRIMARY KEY CLUSTERED ([EmployeeEmploymentId]),
    CONSTRAINT [UQ_EmployeeEmployment_Company_Record] UNIQUE ([CompanyId], [EmployeeEmploymentId]),
    CONSTRAINT [FK_EmployeeEmployment_Employee] FOREIGN KEY ([CompanyId], [EmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_EmployeeEmployment_Department] FOREIGN KEY ([CompanyId], [DepartmentId]) REFERENCES [hrms].[Departments] ([CompanyId], [DepartmentId]),
    CONSTRAINT [FK_EmployeeEmployment_Office] FOREIGN KEY ([CompanyId], [OfficeId]) REFERENCES [hrms].[Offices] ([CompanyId], [OfficeId]),
    CONSTRAINT [FK_EmployeeEmployment_Designation] FOREIGN KEY ([CompanyId], [DesignationId]) REFERENCES [hrms].[Designations] ([CompanyId], [DesignationId]),
    CONSTRAINT [CK_EmployeeEmployment_Type] CHECK ([EmploymentType] IN ('Permanent', 'Contract', 'Temporary', 'Intern', 'Consultant', 'PartTime')),
    CONSTRAINT [CK_EmployeeEmployment_Status] CHECK ([EmploymentStatus] IN ('Active', 'OnLeave', 'Inactive', 'Ended')),
    CONSTRAINT [CK_EmployeeEmployment_Dates] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);
GO

CREATE INDEX [IX_EmployeeEmployment_Company_Employee_Dates]
    ON [hrms].[EmployeeEmployment] ([CompanyId], [EmployeeId], [StartDate] DESC)
    INCLUDE ([EndDate], [DepartmentId], [OfficeId], [DesignationId], [EmploymentStatus])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE UNIQUE INDEX [UX_EmployeeEmployment_OneOpenPrimary]
    ON [hrms].[EmployeeEmployment] ([CompanyId], [EmployeeId])
    WHERE [IsPrimary] = 1 AND [EndDate] IS NULL AND [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[EmployeeReportingAssignments]
(
    [EmployeeReportingAssignmentId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeReportingAssignments_Id] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ManagerEmployeeId] uniqueidentifier NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeReportingAssignments_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeReportingAssignments] PRIMARY KEY CLUSTERED ([EmployeeReportingAssignmentId]),
    CONSTRAINT [FK_EmployeeReportingAssignments_Employee] FOREIGN KEY ([CompanyId], [EmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [FK_EmployeeReportingAssignments_Manager] FOREIGN KEY ([CompanyId], [ManagerEmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_EmployeeReportingAssignments_NoSelfManager] CHECK ([EmployeeId] <> [ManagerEmployeeId]),
    CONSTRAINT [CK_EmployeeReportingAssignments_Dates] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);
GO

CREATE INDEX [IX_EmployeeReportingAssignments_Company_Manager]
    ON [hrms].[EmployeeReportingAssignments] ([CompanyId], [ManagerEmployeeId], [StartDate] DESC)
    INCLUDE ([EmployeeId], [EndDate])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE UNIQUE INDEX [UX_EmployeeReportingAssignments_OneOpenManager]
    ON [hrms].[EmployeeReportingAssignments] ([CompanyId], [EmployeeId])
    WHERE [EndDate] IS NULL AND [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[EmployeeContacts]
(
    [EmployeeContactId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeContacts_Id] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ContactType] varchar(20) NOT NULL,
    [ContactValue] nvarchar(254) NOT NULL,
    [IsPrimary] bit NOT NULL CONSTRAINT [DF_EmployeeContacts_IsPrimary] DEFAULT (0),
    [IsVerified] bit NOT NULL CONSTRAINT [DF_EmployeeContacts_IsVerified] DEFAULT (0),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeContacts_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeContacts] PRIMARY KEY CLUSTERED ([EmployeeContactId]),
    CONSTRAINT [FK_EmployeeContacts_Employee] FOREIGN KEY ([CompanyId], [EmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_EmployeeContacts_Type] CHECK ([ContactType] IN ('WorkEmail', 'PersonalEmail', 'Mobile', 'Phone', 'Emergency')),
    CONSTRAINT [CK_EmployeeContacts_Value_NotBlank] CHECK (LEN(LTRIM(RTRIM([ContactValue]))) > 0)
);
GO

CREATE INDEX [IX_EmployeeContacts_Company_Employee]
    ON [hrms].[EmployeeContacts] ([CompanyId], [EmployeeId], [ContactType])
    INCLUDE ([ContactValue], [IsPrimary], [IsVerified])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE UNIQUE INDEX [UX_EmployeeContacts_OnePrimaryPerType]
    ON [hrms].[EmployeeContacts] ([CompanyId], [EmployeeId], [ContactType])
    WHERE [IsPrimary] = 1 AND [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[EmployeeAddresses]
(
    [EmployeeAddressId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeAddresses_Id] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [AddressType] varchar(20) NOT NULL,
    [AddressLine1] nvarchar(200) NOT NULL,
    [AddressLine2] nvarchar(200) NULL,
    [City] nvarchar(100) NOT NULL,
    [StateOrProvince] nvarchar(100) NULL,
    [PostalCode] nvarchar(20) NULL,
    [CountryCode] char(2) NOT NULL,
    [IsPrimary] bit NOT NULL CONSTRAINT [DF_EmployeeAddresses_IsPrimary] DEFAULT (0),
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeAddresses_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeAddresses] PRIMARY KEY CLUSTERED ([EmployeeAddressId]),
    CONSTRAINT [FK_EmployeeAddresses_Employee] FOREIGN KEY ([CompanyId], [EmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_EmployeeAddresses_Type] CHECK ([AddressType] IN ('Residential', 'Permanent', 'Mailing', 'Other')),
    CONSTRAINT [CK_EmployeeAddresses_Line1_NotBlank] CHECK (LEN(LTRIM(RTRIM([AddressLine1]))) > 0),
    CONSTRAINT [CK_EmployeeAddresses_City_NotBlank] CHECK (LEN(LTRIM(RTRIM([City]))) > 0)
);
GO

CREATE INDEX [IX_EmployeeAddresses_Company_Employee]
    ON [hrms].[EmployeeAddresses] ([CompanyId], [EmployeeId], [AddressType])
    INCLUDE ([City], [CountryCode], [IsPrimary])
    WHERE [DeletedAtUtc] IS NULL;
GO

CREATE UNIQUE INDEX [UX_EmployeeAddresses_OnePrimaryPerType]
    ON [hrms].[EmployeeAddresses] ([CompanyId], [EmployeeId], [AddressType])
    WHERE [IsPrimary] = 1 AND [DeletedAtUtc] IS NULL;
GO

CREATE TABLE [hrms].[EmployeeDocuments]
(
    [EmployeeDocumentId] uniqueidentifier NOT NULL CONSTRAINT [DF_EmployeeDocuments_Id] DEFAULT NEWSEQUENTIALID(),
    [CompanyId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(50) NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(150) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [StorageProvider] varchar(30) NOT NULL,
    [StorageKey] nvarchar(500) NOT NULL,
    [ChecksumSha256] char(64) NULL,
    [UploadedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeDocuments_UploadedAtUtc] DEFAULT SYSUTCDATETIME(),
    [UploadedByUserId] uniqueidentifier NULL,
    [CreatedAtUtc] datetime2(7) NOT NULL CONSTRAINT [DF_EmployeeDocuments_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [CreatedByUserId] uniqueidentifier NULL,
    [UpdatedAtUtc] datetime2(7) NULL,
    [UpdatedByUserId] uniqueidentifier NULL,
    [DeletedAtUtc] datetime2(7) NULL,
    [DeletedByUserId] uniqueidentifier NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY CLUSTERED ([EmployeeDocumentId]),
    CONSTRAINT [FK_EmployeeDocuments_Employee] FOREIGN KEY ([CompanyId], [EmployeeId]) REFERENCES [hrms].[Employees] ([CompanyId], [EmployeeId]),
    CONSTRAINT [CK_EmployeeDocuments_FileSize] CHECK ([FileSizeBytes] > 0),
    CONSTRAINT [CK_EmployeeDocuments_StorageProvider] CHECK ([StorageProvider] IN ('AzureBlob', 'AmazonS3', 'FileSystem')),
    CONSTRAINT [CK_EmployeeDocuments_OriginalFileName_NotBlank] CHECK (LEN(LTRIM(RTRIM([OriginalFileName]))) > 0),
    CONSTRAINT [CK_EmployeeDocuments_StorageKey_NotBlank] CHECK (LEN(LTRIM(RTRIM([StorageKey]))) > 0)
);
GO

CREATE INDEX [IX_EmployeeDocuments_Company_Employee]
    ON [hrms].[EmployeeDocuments] ([CompanyId], [EmployeeId], [UploadedAtUtc] DESC)
    INCLUDE ([DocumentType], [OriginalFileName], [FileSizeBytes])
    WHERE [DeletedAtUtc] IS NULL;
GO
