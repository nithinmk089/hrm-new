/*
    Migration: 026_TimesheetPermissions
    Adds permissions for employee timesheets and manager approvals.
    PermissionId is an identity column in security.Permissions; omit it on insert.
*/
SET NOCOUNT ON;

INSERT INTO security.Permissions
    (PermissionCode, PermissionName, ModuleCode, Description, IsSystemPermission, IsActive)
SELECT source.PermissionCode, source.PermissionName, 'TIMESHEET', source.Description, 1, 1
FROM (VALUES
    ('timesheets.read', N'Read timesheets', N'View timesheets within authorized employee and manager scope.'),
    ('timesheets.manage', N'Manage own timesheets', N'Create and maintain the authenticated employee''s draft or rejected timesheets.'),
    ('timesheets.submit', N'Submit timesheets', N'Submit the authenticated employee''s timesheet for manager review.'),
    ('timesheets.approve', N'Approve timesheets', N'Approve a direct report''s submitted timesheet.'),
    ('timesheets.reject', N'Reject timesheets', N'Reject a direct report''s submitted timesheet.')
) AS source(PermissionCode, PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1
    FROM security.Permissions existing
    WHERE existing.PermissionCode = source.PermissionCode
);
