/*
    Migration: 018_LeaveManagementPermissions
    Adds permissions for working-week setup, employee mappings, balances and leave workflows.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'LEAVE', source.Description, 1, 1
FROM (VALUES
    ('working-week.read', N'Read working week', N'View company and office working-week configuration.'),
    ('working-week.update', N'Update working week', N'Manage company defaults and office working-week overrides.'),
    ('employee-mappings.read', N'Read employee user mappings', N'View employee-to-user identity mappings.'),
    ('employee-mappings.manage', N'Manage employee user mappings', N'Create or deactivate employee-to-user identity mappings.'),
    ('leave-balances.read', N'Read leave balances', N'View employee leave balances within authorized company scope.'),
    ('leave-balances.adjust', N'Adjust leave balances', N'Grant or adjust employee leave entitlement balances.'),
    ('leave-requests.read', N'Read leave requests', N'View leave requests within authorized company scope.'),
    ('leave-requests.create', N'Create leave requests', N'Submit a leave request for the authenticated employee.'),
    ('leave-requests.approve', N'Approve leave requests', N'Approve a leave request assigned to the authenticated manager.'),
    ('leave-requests.reject', N'Reject leave requests', N'Reject a leave request assigned to the authenticated manager.'),
    ('leave-requests.cancel', N'Cancel leave requests', N'Cancel an eligible leave request for the authenticated employee.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM security.Permissions existing
    WHERE existing.PermissionCode = source.PermissionCode
);
