/*
    Migration: 016_LeaveTypePermissions
    Adds company-scoped permissions for leave-type configuration.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'LEAVE', source.Description, 1, 1
FROM (VALUES
    ('leave-types.read', N'Read leave types', N'View leave-type configuration within the authenticated tenant and company scope.'),
    ('leave-types.create', N'Create leave types', N'Create leave types within an authorized company.'),
    ('leave-types.update', N'Update leave types', N'Update leave types within an authorized company.'),
    ('leave-types.deactivate', N'Deactivate leave types', N'Deactivate leave types within an authorized company.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM security.Permissions existing
    WHERE existing.PermissionCode = source.PermissionCode
);
