using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>
/// The last step of a system admin's deletion of a user, once the user has left every tenant: the catalog erases the user's email address
/// and names and marks the user deleted (A8). The row stays, because audit entries point to it. The identity provider deletes its user
/// after the commit. It runs without a tenant, with the user declared (R11).
/// </summary>
public sealed record DeleteUserCommand(Guid UserId, string AdminId);

// The command locks the user row. A membership that appeared after the user left their tenants, such as an invitation accepted at the
// same moment, stops it: a deleted user is never a member (A8). A user already deleted answers as they are, and is not announced again;
// the identity provider is asked again to delete its user, so a deletion that failed there for good can be repeated. Every rejection
// returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class DeleteUserHandler
{
    public static async Task<(Result, UserDeletedEvent?, DeleteIdentityAccountCommand?)> HandleAsync(
        DeleteUserCommand command,
        IUserCatalog users,
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
        var first = user.Delete(now);
        var admin = await users.FindUserIdAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await users.SaveChangesAsync(cancellationToken);

        return (
            Result.Success(),
            first ? new UserDeletedEvent(Guid.CreateVersion7(now), now, user.Id, admin) : null,
            new DeleteIdentityAccountCommand(user.ExternalId));
    }
}
