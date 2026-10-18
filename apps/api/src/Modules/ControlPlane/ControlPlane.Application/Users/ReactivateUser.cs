using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>A system admin reactivates a suspended catalog user. It runs without a tenant.</summary>
public sealed record ReactivateUserCommand(Guid UserId, string AdminId);

// The command sets a state (I1): a user who is not suspended answers as they are, and nothing is announced.
public static class ReactivateUserHandler
{
    public static async Task<(Result<UserDetails>, UserReactivatedEvent?)> HandleAsync(
        ReactivateUserCommand command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        if (!user.Reactivate())
        {
            return (UserDetails.Of(user, isSystemAdmin), null);
        }

        var admin = await users.FindUserIdAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (UserDetails.Of(user, isSystemAdmin), new UserReactivatedEvent(Guid.CreateVersion7(now), now, user.Id, admin));
    }
}
