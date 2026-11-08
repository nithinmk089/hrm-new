/*
    Migration: 007_CompanyPermissions
    Adds company-management permission codes to the canonical catalogue.
    Existing permission records and IDs are preserved.
*/
SET NOCOUNT ON;
INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'COMPANY', source.Description, 1, 1
FROM (VALUES
    ('companies.read', N'Read companies', N'View company records within the authenticated tenant and assigned organizational scope.'),
    ('companies.create', N'Create companies', N'Create a company within the authenticated tenant.'),
    ('companies.update', N'Update companies', N'Update company details within the authenticated tenant and assigned company scope.'),
    ('companies.deactivate', N'Deactivate companies', N'Deactivate a company within the authenticated tenant and assigned company scope.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS (SELECT 1 FROM security.Permissions existing WHERE existing.PermissionCode = source.PermissionCode);
