using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// A system admin suspends a catalog user: the user is refused on every me and tenant endpoint until they are reactivated (A9). The
/// identity provider is not told. It runs without a tenant.
/// </summary>
public sealed record SuspendUserCommand(Guid UserId, string AdminId);

// The command sets a state (I1): a user already suspended answers as they are, keeps the first time, and nothing is announced again. A
// system admin is never suspended, the admin who calls included, so the system door stays open to system admins. Every rejection returns
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

        var admin = await users.FindUserIdAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        var now = time.GetUtcNow();
        if (!user.Suspend(now, admin))
        {
            return (UserView.Of(user, isSystemAdmin: false), null);
        }

        await users.SaveChangesAsync(cancellationToken);
        return (UserView.Of(user, isSystemAdmin: false), new UserSuspendedEvent(Guid.CreateVersion7(now), now, user.Id, admin));
    }
}
