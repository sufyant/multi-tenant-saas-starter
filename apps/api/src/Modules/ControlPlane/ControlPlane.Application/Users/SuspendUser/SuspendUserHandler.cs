using ControlPlane.Application.Ports;
using ControlPlane.Contracts.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.SuspendUser;

// The command sets a state (I1): a user already suspended answers as they are, keeps the first time, and nothing is announced again. A
// system admin is never suspended, the system admin who calls included, so the system door stays open to system admins. Every rejection returns
// before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class SuspendUserHandler
{
    public static async Task<(Result<UserView>, UserSuspendedEvent?)> HandleAsync(
        SuspendUserCommand command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        if (await users.IsSystemAdminAsync(user.Id, cancellationToken))
        {
            return (Errors.UserIsSystemAdmin, null);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        var now = time.GetUtcNow();
        if (user.Suspend(now, actor) == StateChange.Unchanged)
        {
            return (UserView.Of(user, isSystemAdmin: false), null);
        }

        await users.SaveChangesAsync(cancellationToken);
        return (UserView.Of(user, isSystemAdmin: false), new UserSuspendedEvent(Guid.CreateVersion7(now), now, user.Id, actor));
    }
}
