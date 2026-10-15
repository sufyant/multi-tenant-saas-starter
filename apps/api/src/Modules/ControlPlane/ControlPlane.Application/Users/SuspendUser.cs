using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// A system admin suspends a catalog user: the user is refused on every me and tenant endpoint until they are reactivated (A9). The
/// identity provider is not told. It runs without a tenant.
/// </summary>
public sealed record SuspendUser(Guid UserId);

// The command sets a state (I1): a user already suspended answers as they are and keeps the first time. A system admin is never
// suspended, the admin who calls included, so the system door stays open to staff. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7).
public static class SuspendUserHandler
{
    public static async Task<Result<UserDetails>> HandleAsync(
        SuspendUser command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        if (await users.IsSystemAdminAsync(user.Id, cancellationToken))
        {
            return Error.Conflict("user.is_system_admin", "A system admin cannot be suspended.");
        }

        user.Suspend(time.GetUtcNow());
        await users.SaveChangesAsync(cancellationToken);
        return UserDetails.Of(user, isSystemAdmin: false);
    }
}
