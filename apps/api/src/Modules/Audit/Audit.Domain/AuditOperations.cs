namespace Audit.Domain;

/// <summary>
/// The operation of every audit entry, each declared once (K3). The OpenAPI document reads these constants into the enum
/// AuditOperation, so a client knows every operation. An entry keeps its operation as text, and a stored name never changes.
/// </summary>
public static class AuditOperations
{
    public const string InvitationRevoked = "invitation.revoked";

    public const string MemberInvited = "member.invited";

    public const string MemberJoined = "member.joined";

    public const string MemberRemoved = "member.removed";

    public const string MemberRoleChanged = "member.role_changed";

    public const string RoleCreated = "role.created";

    public const string RoleDeleted = "role.deleted";

    public const string RoleUpdated = "role.updated";

    public const string SystemAdminGranted = "system_admin.granted";

    public const string SystemAdminRevoked = "system_admin.revoked";

    public const string TenantClosed = "tenant.closed";

    public const string TenantCreated = "tenant.created";

    public const string TenantOwnerInvited = "tenant.owner_invited";

    public const string TenantOwnerMade = "tenant.owner_made";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantPurgeWarned = "tenant.purge_warned";

    public const string TenantPurged = "tenant.purged";

    public const string TenantRenamed = "tenant.renamed";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantSuspended = "tenant.suspended";

    public const string UserDeleted = "user.deleted";

    public const string UserPreferencesChanged = "user.preferences_changed";

    public const string UserReactivated = "user.reactivated";

    public const string UserRenamed = "user.renamed";

    public const string UserSuspended = "user.suspended";
}
