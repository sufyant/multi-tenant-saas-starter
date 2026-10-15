using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>A system admin reactivates a suspended catalog user. It runs without a tenant.</summary>
public sealed record ReactivateUser(Guid UserId);

// The command sets a state (I1): a user who is not suspended answers as they are.
public static class ReactivateUserHandler
{
    public static async Task<Result<UserDetails>> HandleAsync(ReactivateUser command, IUserCatalog users, CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        user.Reactivate();
        await users.SaveChangesAsync(cancellationToken);
        return UserDetails.Of(user, await users.IsSystemAdminAsync(user.Id, cancellationToken));
    }
}
