/*
    Migration: 006_RefreshTokenFamilyAndMembership
    Purpose:
      - Bind each canonical refresh token to the exact tenant membership.
      - Add a token-family identifier for atomic rotation and reuse detection.
      - Preserve existing refresh-token hashes and revocation history.

    Notes:
      - Migrations 001-005 are immutable.
      - Existing tokens are assigned to their unique tenant membership.
      - Existing token families are initialized to the existing token ID.
      - The migration stops if any existing token cannot be mapped safely.
*/

SET XACT_ABORT ON;

IF OBJECT_ID(N'security.RefreshTokens', N'U') IS NULL
    THROW 51060, 'Required table security.RefreshTokens does not exist.', 1;

IF OBJECT_ID(N'security.UserTenantMemberships', N'U') IS NULL
    THROW 51061, 'Required table security.UserTenantMemberships does not exist.', 1;

IF COL_LENGTH(N'security.RefreshTokens', N'TenantId') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'UserId') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'RefreshTokenId') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'TokenHash') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'ExpiresAtUtc') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'RevokedAtUtc') IS NULL
   OR COL_LENGTH(N'security.RefreshTokens', N'ReplacedByTokenId') IS NULL
    THROW 51062, 'security.RefreshTokens does not match the expected canonical schema.', 1;

IF COL_LENGTH(N'security.RefreshTokens', N'MembershipId') IS NULL
    ALTER TABLE security.RefreshTokens ADD MembershipId uniqueidentifier NULL;

IF COL_LENGTH(N'security.RefreshTokens', N'TokenFamilyId') IS NULL
    ALTER TABLE security.RefreshTokens ADD TokenFamilyId uniqueidentifier NULL;

-- Every refresh token must map to exactly one membership (enforced by
-- UQ_UserTenantMemberships_Tenant_User from migration 005).
UPDATE rt
SET
    MembershipId = membership.MembershipId,
    TokenFamilyId = COALESCE(rt.TokenFamilyId, rt.RefreshTokenId)
FROM security.RefreshTokens AS rt
INNER JOIN security.UserTenantMemberships AS membership
    ON membership.TenantId = rt.TenantId
   AND membership.UserId = rt.UserId
WHERE rt.MembershipId IS NULL
   OR rt.TokenFamilyId IS NULL;

IF EXISTS
(
    SELECT 1
    FROM security.RefreshTokens
    WHERE MembershipId IS NULL OR TokenFamilyId IS NULL
)
    THROW 51063, 'One or more refresh tokens could not be mapped to a tenant membership. Review the records before retrying migration 006.', 1;

-- Make the backfilled identity mandatory before adding the composite FK.
ALTER TABLE security.RefreshTokens
    ALTER COLUMN MembershipId uniqueidentifier NOT NULL;

ALTER TABLE security.RefreshTokens
    ALTER COLUMN TokenFamilyId uniqueidentifier NOT NULL;

-- The composite FK proves that the stored membership belongs to the same
-- tenant and user as the refresh-token record.
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'security.UserTenantMemberships')
      AND name = N'UX_UserTenantMemberships_Tenant_Membership_User'
)
    CREATE UNIQUE INDEX UX_UserTenantMemberships_Tenant_Membership_User
        ON security.UserTenantMemberships (TenantId, MembershipId, UserId);

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'security.RefreshTokens')
      AND name = N'FK_RefreshTokens_TenantMembershipIdentity'
)
    ALTER TABLE security.RefreshTokens
        ADD CONSTRAINT FK_RefreshTokens_TenantMembershipIdentity
        FOREIGN KEY (TenantId, MembershipId, UserId)
        REFERENCES security.UserTenantMemberships (TenantId, MembershipId, UserId);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'security.RefreshTokens')
      AND name = N'IX_RefreshTokens_Tenant_User_Family'
)
    CREATE INDEX IX_RefreshTokens_Tenant_User_Family
        ON security.RefreshTokens (TenantId, UserId, TokenFamilyId)
        INCLUDE (RefreshTokenId, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenId);

GO
