using SharedKernel;

namespace ControlPlane.Application;

internal static class Errors
{
    public static readonly Error InvitationNotFound = Error.NotFound("invitation.not_found", "The invitation was not found.");

    public static readonly Error MembershipNotFound = Error.NotFound("membership.not_found", "The member was not found.");

    public static readonly Error TenantNotFound = Error.NotFound("tenant.not_found", "The tenant was not found.");

    public static readonly Error UserNotFound = Error.NotFound("user.not_found", "The user was not found.");
}
