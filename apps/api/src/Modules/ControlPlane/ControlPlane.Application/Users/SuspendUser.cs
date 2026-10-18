using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// A system admin suspends a catalog user: the user is refused on every me and tenant endpoint until they are reactivated (A9). The
/// identity provider is not told. It runs without a tenant.
/// </summary>
public sealed record SuspendUser(Guid UserId, string AdminId);

// The command sets a state (I1): a user already suspended answers as they are, keeps the first time, and nothing is announced again. A
// system admin is never suspended, the admin who calls included, so the system door stays open to system admins. Every rejection returns
// before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class SuspendUserHandler
{
    public static async Task<(Result<UserDetails>, UserSuspended?)> HandleAsync(
        SuspendUser command,
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
            return (Error.Conflict(ControlPlaneErrorCodes.UserIsSystemAdmin, "A system admin cannot be suspended."), null);
        }

        var admin = await users.FindUserIdAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        var now = time.GetUtcNow();
        if (!user.Suspend(now, admin))
        {
            return (UserDetails.Of(user, isSystemAdmin: false), null);
        }

        await users.SaveChangesAsync(cancellationToken);
        return (UserDetails.Of(user, isSystemAdmin: false), new UserSuspended(Guid.CreateVersion7(now), now, user.Id, admin));
    }
}
