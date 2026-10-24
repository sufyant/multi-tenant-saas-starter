using SharedKernel;

namespace ControlPlane.Domain;

/// <summary>
/// The codes of every error ControlPlane answers, in its lowest layer, so that every layer of the module reaches them (K5).
/// </summary>
public static class ControlPlaneErrorCodes
{
    public static readonly ErrorCode InvitationCodeRequired = ErrorCode.Declare("invitation.code_required");

    public static readonly ErrorCode InvitationDailyLimitReached = ErrorCode.Declare("invitation.daily_limit_reached");

    public static readonly ErrorCode InvitationEmailMismatch = ErrorCode.Declare("invitation.email_mismatch");

    public static readonly ErrorCode InvitationExpired = ErrorCode.Declare("invitation.expired");

    public static readonly ErrorCode InvitationNotFound = ErrorCode.Declare("invitation.not_found");

    public static readonly ErrorCode InvitationNotPending = ErrorCode.Declare("invitation.not_pending");

    public static readonly ErrorCode InvitationOwnerRequiresOwner = ErrorCode.Declare("invitation.owner_requires_owner");

    public static readonly ErrorCode InvitationSystemAdminCannotJoin = ErrorCode.Declare("invitation.system_admin_cannot_join");

    public static readonly ErrorCode MemberAlreadyOwner = ErrorCode.Declare("member.already_owner");

    public static readonly ErrorCode MemberCannotRemoveSelf = ErrorCode.Declare("member.cannot_remove_self");

    public static readonly ErrorCode MemberExists = ErrorCode.Declare("member.exists");

    public static readonly ErrorCode MemberLastOwner = ErrorCode.Declare("member.last_owner");

    public static readonly ErrorCode MemberNotFound = ErrorCode.Declare("member.not_found");

    public static readonly ErrorCode MemberOwnerRequiresOwner = ErrorCode.Declare("member.owner_requires_owner");

    public static readonly ErrorCode RoleBuiltIn = ErrorCode.Declare("role.built_in");

    public static readonly ErrorCode RoleEscalation = ErrorCode.Declare("role.escalation");

    public static readonly ErrorCode RoleInUse = ErrorCode.Declare("role.in_use");

    public static readonly ErrorCode RoleLimitReached = ErrorCode.Declare("role.limit_reached");

    public static readonly ErrorCode RoleNameInvalid = ErrorCode.Declare("role.name_invalid");

    public static readonly ErrorCode RoleNameTaken = ErrorCode.Declare("role.name_taken");

    public static readonly ErrorCode RoleNotFound = ErrorCode.Declare("role.not_found");

    public static readonly ErrorCode RoleNotPermitted = ErrorCode.Declare("role.not_permitted");

    public static readonly ErrorCode RolePermissionInvalid = ErrorCode.Declare("role.permission_invalid");

    public static readonly ErrorCode RolePermissionOwnerOnly = ErrorCode.Declare("role.permission_owner_only");

    public static readonly ErrorCode RolePermissionsRequired = ErrorCode.Declare("role.permissions_required");

    public static readonly ErrorCode RoleProfileReadRequired = ErrorCode.Declare("role.profile_read_required");

    public static readonly ErrorCode RoleRolesReadRequired = ErrorCode.Declare("role.roles_read_required");

    public static readonly ErrorCode SystemAdminCannotRevokeSelf = ErrorCode.Declare("system_admin.cannot_revoke_self");

    public static readonly ErrorCode SystemAdminExists = ErrorCode.Declare("system_admin.exists");

    public static readonly ErrorCode SystemAdminNotFound = ErrorCode.Declare("system_admin.not_found");

    public static readonly ErrorCode SystemAdminReverificationRequired = ErrorCode.Declare("system_admin.reverification_required");

    public static readonly ErrorCode SystemAdminRoot = ErrorCode.Declare("system_admin.root");

    public static readonly ErrorCode TenantClosed = ErrorCode.Declare("tenant.closed");

    public static readonly ErrorCode TenantHasOwner = ErrorCode.Declare("tenant.has_owner");

    public static readonly ErrorCode TenantNameInvalid = ErrorCode.Declare("tenant.name_invalid");

    public static readonly ErrorCode TenantNotActive = ErrorCode.Declare("tenant.not_active");

    public static readonly ErrorCode TenantNotClosed = ErrorCode.Declare("tenant.not_closed");

    public static readonly ErrorCode TenantNotFound = ErrorCode.Declare("tenant.not_found");

    public static readonly ErrorCode TenantReopenPeriodEnded = ErrorCode.Declare("tenant.reopen_period_ended");

    public static readonly ErrorCode TenantSlugInvalid = ErrorCode.Declare("tenant.slug_invalid");

    public static readonly ErrorCode TenantSlugTaken = ErrorCode.Declare("tenant.slug_taken");

    public static readonly ErrorCode TenantSuspended = ErrorCode.Declare("tenant.suspended");

    public static readonly ErrorCode UserDeleted = ErrorCode.Declare("user.deleted");

    public static readonly ErrorCode UserHasMemberships = ErrorCode.Declare("user.has_memberships");

    public static readonly ErrorCode UserIsSystemAdmin = ErrorCode.Declare("user.is_system_admin");

    public static readonly ErrorCode UserLastOwner = ErrorCode.Declare("user.last_owner");

    public static readonly ErrorCode UserNotFound = ErrorCode.Declare("user.not_found");

    public static readonly ErrorCode UserSuspended = ErrorCode.Declare("user.suspended");
}
