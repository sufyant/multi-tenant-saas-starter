using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application.Users.DeleteUser;

// The command locks the user row. A membership that appeared after the user left their tenants, such as an invitation accepted at the
// same moment, stops it: a deleted user is never a member (A8). A user already deleted answers as they are, and is not announced again;
// the identity provider is asked again to delete its user, so a deletion that failed there for good can be repeated. Rejections come
// first (W7).
public static class DeleteUserHandler
{
    public static async Task<(Result, UserDeletedEvent?, DeleteIdentityAccountCommand?)> HandleAsync(
        DeleteUserCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null, null);
        }

        if (await users.IsSystemAdminAsync(user.Id, cancellationToken))
        {
            return (Errors.UserIsSystemAdmin, null, null);
        }

        if ((await users.ListMembershipsAsync(user.Id, cancellationToken)).Count > 0)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.UserHasMemberships, "The user joined a tenant while the deletion ran; delete them again."), null, null);
        }

        var now = time.GetUtcNow();
        var deleted = user.Delete(now);
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await users.SaveChangesAsync(cancellationToken);

        return (
            Result.Success(),
            deleted == StateChange.Changed ? new UserDeletedEvent(Guid.CreateVersion7(now), now, user.Id, actor) : null,
            new DeleteIdentityAccountCommand(user.ExternalId));
    }
}
