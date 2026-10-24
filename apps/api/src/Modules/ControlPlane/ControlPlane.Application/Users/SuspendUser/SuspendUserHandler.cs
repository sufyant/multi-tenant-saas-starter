using ControlPlane.Application.SystemAdmins;
using ControlPlane.Contracts.Users;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.SuspendUser;

// The command sets a state (I1): a user already suspended answers as they are, keeps the first time, and nothing is announced again. A
// system admin is never suspended, the system admin who calls included, so the system door stays open to system admins. Rejections come
// first (W7).
public static class SuspendUserHandler
{
    public static async Task<(Result<UserView>, OutgoingMessages)> HandleAsync(
        SuspendUserCommand command,
        IUserRepository users,
        SystemAdminOptions systemAdmins,
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

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var now = time.GetUtcNow();
        if (user.Suspend(now, actor) == StateChange.Unchanged)
        {
            return (UserView.Of(user, systemAdminGrantedAt: null, systemAdmins), []);
        }

        await users.SaveChangesAsync(cancellationToken);
        return (UserView.Of(user, systemAdminGrantedAt: null, systemAdmins), [new UserSuspendedEvent(Guid.CreateVersion7(now), now, user.Id, actor)]);
    }
}
