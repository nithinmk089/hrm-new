/*
    Migration: 026_TimesheetPermissions
    Purpose: Register permissions for employee timesheets and manager approvals.
*/
INSERT INTO security.Permissions
    (PermissionId, PermissionCode, PermissionName, Description, ModuleCode, IsActive)
SELECT NEWID(), source.PermissionCode, source.PermissionName, source.Description, 'TIMESHEET', 1
FROM
(
    VALUES
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
GO
