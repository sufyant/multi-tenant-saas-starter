using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Application.Users.EraseFormerMember;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.DeleteUser;

// The command locks the user row. A membership that appeared after the user left their tenants, such as an invitation accepted at the
// same moment, stops it: a deleted user is never a member (A8). A user already deleted answers as they are, and is not announced again;
// the identity provider is asked again to delete its user, so a deletion that failed there for good can be repeated. Rejections come
// first (W7). Under the same lock the tenants the user joined are listed, all of them left now, each of which then erases the user's
// address from its invitations in a transaction of its own (A8, EraseFormerMemberCommand), with the address read before the erasure.
public static class DeleteUserHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        DeleteUserCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        if (await users.IsSystemAdminAsync(user.Id, cancellationToken))
        {
            return (Errors.UserIsSystemAdmin, []);
        }

        if ((await users.ListMembershipsAsync(user.Id, cancellationToken)).Count > 0)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.UserHasMemberships, "The user joined a tenant while the deletion ran; delete them again."), []);
        }

        var now = time.GetUtcNow();
        var email = user.Email;
        var joined = await users.ListJoinedTenantsAsync(user.Id, cancellationToken);
        var deleted = user.Delete(now);
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await users.SaveChangesAsync(cancellationToken);

        OutgoingMessages messages = [new DeleteIdentityAccountCommand(user.ExternalId), .. EraseFormerMemberCommand.For(user.Id, email, joined)];
        if (deleted == StateChange.Changed)
        {
            messages.Insert(0, new UserDeletedEvent(Guid.CreateVersion7(now), now, user.Id, actor));
        }

        return (Result.Success(), messages);
    }
}
