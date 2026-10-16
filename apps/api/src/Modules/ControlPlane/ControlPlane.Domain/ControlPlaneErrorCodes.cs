using SharedKernel;

namespace ControlPlane.Domain;

/// <summary>
/// The codes of every error ControlPlane answers, in its lowest layer, so that every layer of the module reaches them (K5). The
/// webhook's code is here too, although the webhooks are not in the OpenAPI document (N5).
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

    public static readonly ErrorCode InvitationStaffCannotJoin = ErrorCode.Declare("invitation.staff_cannot_join");

    public static readonly ErrorCode MembershipCannotRemoveSelf = ErrorCode.Declare("membership.cannot_remove_self");

    public static readonly ErrorCode MembershipExists = ErrorCode.Declare("membership.exists");

    public static readonly ErrorCode MembershipLastOwner = ErrorCode.Declare("membership.last_owner");

    public static readonly ErrorCode MembershipNotFound = ErrorCode.Declare("membership.not_found");

    public static readonly ErrorCode MembershipOwnerRequiresOwner = ErrorCode.Declare("membership.owner_requires_owner");

    public static readonly ErrorCode TenantClosed = ErrorCode.Declare("tenant.closed");

    public static readonly ErrorCode TenantNameInvalid = ErrorCode.Declare("tenant.name_invalid");

    public static readonly ErrorCode TenantNotActive = ErrorCode.Declare("tenant.not_active");

    public static readonly ErrorCode TenantNotClosed = ErrorCode.Declare("tenant.not_closed");

    public static readonly ErrorCode TenantNotFound = ErrorCode.Declare("tenant.not_found");

    public static readonly ErrorCode TenantReopenPeriodEnded = ErrorCode.Declare("tenant.reopen_period_ended");

    public static readonly ErrorCode TenantSlugInvalid = ErrorCode.Declare("tenant.slug_invalid");

    public static readonly ErrorCode TenantSlugTaken = ErrorCode.Declare("tenant.slug_taken");

    public static readonly ErrorCode TenantSuspended = ErrorCode.Declare("tenant.suspended");

    public static readonly ErrorCode UserIsSystemAdmin = ErrorCode.Declare("user.is_system_admin");

    public static readonly ErrorCode UserNotFound = ErrorCode.Declare("user.not_found");

    public static readonly ErrorCode UserSuspended = ErrorCode.Declare("user.suspended");

    public static readonly ErrorCode WebhookSignatureInvalid = ErrorCode.Declare("webhook.signature_invalid");
}
