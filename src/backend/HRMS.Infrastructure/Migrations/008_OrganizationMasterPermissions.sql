/*
    Migration: 008_OrganizationMasterPermissions
    Adds tenant/company-scoped permissions for departments, offices and designations.
    Existing permission records and IDs are preserved.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'ORGANIZATION', source.Description, 1, 1
FROM (VALUES
    ('departments.read', N'Read departments', N'View department records within the authenticated tenant and company scope.'),
    ('departments.create', N'Create departments', N'Create departments within an authorized company.'),
    ('departments.update', N'Update departments', N'Update departments within an authorized company.'),
    ('departments.deactivate', N'Deactivate departments', N'Deactivate departments within an authorized company.'),
    ('offices.read', N'Read offices', N'View office records within the authenticated tenant and company scope.'),
    ('offices.create', N'Create offices', N'Create offices within an authorized company.'),
    ('offices.update', N'Update offices', N'Update offices within an authorized company.'),
    ('offices.deactivate', N'Deactivate offices', N'Deactivate offices within an authorized company.'),
    ('designations.read', N'Read designations', N'View designation records within the authenticated tenant and company scope.'),
    ('designations.create', N'Create designations', N'Create designations within an authorized company.'),
    ('designations.update', N'Update designations', N'Update designations within an authorized company.'),
    ('designations.deactivate', N'Deactivate designations', N'Deactivate designations within an authorized company.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1
    FROM security.Permissions existing
    WHERE existing.PermissionCode = source.PermissionCode
);
