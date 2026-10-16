using SharedKernel;

namespace ControlPlane.Application;

internal static class Errors
{
    public static readonly Error InvitationNotFound = Error.NotFound(ErrorCode.InvitationNotFound, "The invitation was not found.");

    public static readonly Error MembershipNotFound = Error.NotFound(ErrorCode.MembershipNotFound, "The member was not found.");

    public static readonly Error TenantNotFound = Error.NotFound(ErrorCode.TenantNotFound, "The tenant was not found.");

    public static readonly Error UserNotFound = Error.NotFound(ErrorCode.UserNotFound, "The user was not found.");
}
