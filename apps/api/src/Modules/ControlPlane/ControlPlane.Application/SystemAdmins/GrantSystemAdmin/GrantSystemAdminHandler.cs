using ControlPlane.Application.Users;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain.SystemAdmins;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.SystemAdmins.GrantSystemAdmin;

// The user row is locked first, so a grant, a revocation, a suspension and a deletion of one user run one at a time (K6a). A deleted, a
// suspended and a system admin user are refused before any change (SystemAdmin.Grant, W7). The grant is no state to set again: a second
// one answers 409, and nothing is announced.
public static class GrantSystemAdminHandler
{
    public static async Task<(Result<UserView>, OutgoingMessages)> HandleAsync(
        GrantSystemAdminCommand command,
        IUserRepository users,
        SystemAdminOptions systemAdmins,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        var now = time.GetUtcNow();
        var granted = SystemAdmin.Grant(user, await users.IsSystemAdminAsync(user.Id, cancellationToken), now);
        if (!granted.IsSuccess)
        {
            return (granted.Error, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        users.AddSystemAdmin(granted.Value);
        await users.SaveChangesAsync(cancellationToken);
        return (UserView.Of(user, granted.Value.GrantedAt, systemAdmins), [new SystemAdminGrantedEvent(Guid.CreateVersion7(now), now, user.Id, actor)]);
    }
}
