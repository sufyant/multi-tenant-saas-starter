using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Domain.SystemAdmins;

/// <summary>
/// A grant that makes a user a system admin: a person who works for the provider. It is an extra grant on the single identity,
/// recorded with when it was granted. The system door grants a root system admin, whom the configuration names; a system admin grants
/// and revokes any other (section 6, "Root system admins").
/// </summary>
internal sealed class SystemAdmin(Guid userId, DateTimeOffset grantedAt)
{
    public Guid UserId { get; private init; } = userId;

    public DateTimeOffset GrantedAt { get; private init; } = grantedAt;

    /// <summary>
    /// The grant for a user, at this moment. A deleted user has no account to sign in with, and a suspended one is reactivated first;
    /// a system admin is one already.
    /// </summary>
    public static Result<SystemAdmin> Grant(User user, bool isSystemAdmin, DateTimeOffset now)
    {
        if (user.Status == UserStatus.Deleted)
        {
            return Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted.");
        }

        if (user.Status == UserStatus.Suspended)
        {
            return Error.Conflict(ControlPlaneErrorCodes.UserSuspended, "The user is suspended.");
        }

        if (isSystemAdmin)
        {
            return Error.Conflict(ControlPlaneErrorCodes.SystemAdminExists, "The user is a system admin already.");
        }

        return new SystemAdmin(user.Id, now);
    }

    /// <summary>
    /// Whether a system admin may take this grant away. A root system admin keeps it while the configuration's list names them, and
    /// nobody locks themself out by a click. Revoking deletes no user and leaves every membership.
    /// </summary>
    public Result CheckRevoke(Guid actorId, bool isRoot)
    {
        if (isRoot)
        {
            return Error.Conflict(ControlPlaneErrorCodes.SystemAdminRoot, "A root system admin cannot be revoked.");
        }

        return actorId == UserId
            ? Error.Conflict(ControlPlaneErrorCodes.SystemAdminCannotRevokeSelf, "A system admin cannot revoke themself.")
            : Result.Success();
    }
}
