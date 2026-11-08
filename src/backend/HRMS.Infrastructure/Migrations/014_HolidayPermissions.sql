/*
    Migration: 014_HolidayPermissions
    Adds company-scoped permissions for holiday management.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'COMPANY', source.Description, 1, 1
FROM (VALUES
    ('holidays.read', N'Read holidays', N'View company holidays within the authenticated tenant and company scope.'),
    ('holidays.create', N'Create holidays', N'Create holidays within an authorized company.'),
    ('holidays.update', N'Update holidays', N'Update holidays within an authorized company.'),
    ('holidays.deactivate', N'Deactivate holidays', N'Deactivate holidays within an authorized company.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM security.Permissions existing
    WHERE existing.PermissionCode = source.PermissionCode
);
