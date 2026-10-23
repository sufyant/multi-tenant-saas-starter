using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Contracts.Users;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.DeleteUserWithoutTenant;

// The erasure step of a user's deletion (section 6, "User deletion", step 3), which the platform takes for a user that a tenant's purge
// left without a tenant. It locks the user row and checks again under the lock: a system admin stays, a user who joined another tenant
// meanwhile stays, and a user already deleted changes nothing. Otherwise it erases the user as DeleteUser does (User.Delete), records
// user.deleted by the platform, and deletes the user at the identity provider after the commit. No caller waits for an answer, so a user
// who stays is no error.
public static class DeleteUserWithoutTenantHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        DeleteUserWithoutTenantCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user
            || await users.IsSystemAdminAsync(user.Id, cancellationToken)
            || (await users.ListMembershipsAsync(user.Id, cancellationToken)).Count > 0)
        {
            return [];
        }

        var now = time.GetUtcNow();
        if (user.Delete(now) == StateChange.Unchanged)
        {
            return [];
        }

        await users.SaveChangesAsync(cancellationToken);
        return [new UserDeletedEvent(Guid.CreateVersion7(now), now, user.Id, null), new DeleteIdentityAccountCommand(user.ExternalId)];
    }
}
