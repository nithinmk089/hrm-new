/*
    HRMS V1 - pre-Migration 005 schema inspection
    READ ONLY: this script does not create, alter, update, or delete database objects/data.

    Run against the same database used by HRMS.API before designing Migration 005.
    Share the complete result sets with the implementation team.
*/

SET NOCOUNT ON;

-- 1. Relevant table columns and types.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrdinal,
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length AS MaxLengthBytes,
    c.precision AS [Precision],
    c.scale AS Scale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    dc.definition AS DefaultDefinition,
    c.is_computed AS IsComputed,
    cc.definition AS ComputedDefinition
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.columns AS c ON c.object_id = t.object_id
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = c.object_id
   AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id
   AND cc.column_id = c.column_id
WHERE (s.name = N'platform' AND t.name = N'Tenants')
   OR (s.name = N'hrms' AND t.name IN
       (N'Companies', N'Offices', N'CompanyUserMemberships', N'Users',
        N'Roles', N'Permissions', N'UserRoles', N'RolePermissions',
        N'RefreshTokens', N'AuditLogs'))
   OR (s.name = N'security' AND t.name IN
       (N'Users', N'Roles', N'Permissions', N'UserRoles',
        N'RolePermissions', N'RefreshTokens'))
   OR (s.name = N'audit' AND t.name = N'AuditLogs')
ORDER BY s.name, t.name, c.column_id;

-- 2. Primary keys, unique constraints, check constraints, and foreign keys.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    kc.name AS ConstraintName,
    kc.type_desc AS ConstraintType,
    STRING_AGG(QUOTENAME(c.name), N', ')
        WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns,
    CAST(NULL AS nvarchar(max)) AS DefinitionOrReference
FROM sys.key_constraints AS kc
INNER JOIN sys.tables AS t ON t.object_id = kc.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = t.object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE (s.name IN (N'platform', N'hrms', N'security', N'audit'))
  AND t.name IN
      (N'Tenants', N'Companies', N'Offices', N'CompanyUserMemberships',
       N'Users', N'Roles', N'Permissions', N'UserRoles',
       N'RolePermissions', N'RefreshTokens', N'AuditLogs')
GROUP BY s.name, t.name, kc.name, kc.type_desc

UNION ALL

SELECT
    s.name,
    t.name,
    fk.name,
    N'FOREIGN_KEY',
    STRING_AGG(QUOTENAME(pc.name), N', ')
        WITHIN GROUP (ORDER BY fkc.constraint_column_id),
    QUOTENAME(rs.name) + N'.' + QUOTENAME(rt.name)
        + N' (' + STRING_AGG(QUOTENAME(rc.name), N', ')
            WITHIN GROUP (ORDER BY fkc.constraint_column_id) + N')'
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.columns AS pc
    ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.tables AS rt ON rt.object_id = fkc.referenced_object_id
INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
INNER JOIN sys.columns AS rc
    ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
WHERE s.name IN (N'platform', N'hrms', N'security', N'audit')
  AND t.name IN
      (N'Tenants', N'Companies', N'Offices', N'CompanyUserMemberships',
       N'Users', N'Roles', N'Permissions', N'UserRoles',
       N'RolePermissions', N'RefreshTokens', N'AuditLogs')
GROUP BY s.name, t.name, fk.name, rs.name, rt.name

UNION ALL

SELECT
    s.name,
    t.name,
    cc.name,
    N'CHECK',
    NULL,
    cc.definition
FROM sys.check_constraints AS cc
INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name IN (N'platform', N'hrms', N'security', N'audit')
  AND t.name IN
      (N'Tenants', N'Companies', N'Offices', N'CompanyUserMemberships',
       N'Users', N'Roles', N'Permissions', N'UserRoles',
       N'RolePermissions', N'RefreshTokens', N'AuditLogs')
ORDER BY SchemaName, TableName, ConstraintType, ConstraintName;

-- 3. Index definitions, including filters and included columns.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    STRING_AGG(
        CASE WHEN ic.is_included_column = 1
             THEN N'INCLUDE ' + QUOTENAME(c.name)
             ELSE QUOTENAME(c.name)
                  + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N' ASC' END
        END,
        N', '
    ) WITHIN GROUP (ORDER BY ic.key_ordinal, ic.index_column_id) AS IndexColumns
FROM sys.indexes AS i
INNER JOIN sys.tables AS t ON t.object_id = i.object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.index_id > 0
  AND s.name IN (N'platform', N'hrms', N'security', N'audit')
  AND t.name IN
      (N'Tenants', N'Companies', N'Offices', N'CompanyUserMemberships',
       N'Users', N'Roles', N'Permissions', N'UserRoles',
       N'RolePermissions', N'RefreshTokens', N'AuditLogs')
GROUP BY s.name, t.name, i.name, i.type_desc, i.is_primary_key,
         i.is_unique, i.has_filter, i.filter_definition
ORDER BY s.name, t.name, i.name;

-- 4. Row counts for relevant tables.
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(p.rows) AS ApproximateRowCount
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
INNER JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE (s.name = N'platform' AND t.name = N'Tenants')
   OR (s.name = N'hrms' AND t.name IN
       (N'Companies', N'Offices', N'CompanyUserMemberships', N'Users',
        N'Roles', N'Permissions', N'UserRoles', N'RolePermissions',
        N'RefreshTokens', N'AuditLogs'))
   OR (s.name = N'security' AND t.name IN
       (N'Users', N'Roles', N'Permissions', N'UserRoles',
        N'RolePermissions', N'RefreshTokens'))
   OR (s.name = N'audit' AND t.name = N'AuditLogs')
GROUP BY s.name, t.name
ORDER BY s.name, t.name;

-- 5. Applied HRMS migrations.
IF OBJECT_ID(N'dbo.__HrmsSchemaMigrations', N'U') IS NOT NULL
BEGIN
    SELECT *
    FROM dbo.__HrmsSchemaMigrations
    ORDER BY 1;
END
ELSE
BEGIN
    SELECT N'dbo.__HrmsSchemaMigrations not found' AS MigrationTableStatus;
END;
