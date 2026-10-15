using ControlPlane.Application.Ports;
using ControlPlane.Contracts;

namespace ControlPlane.Application.Users;

/// <summary>
/// The identity provider deleted the user: the catalog erases its copy of their profile and marks the user deleted. The row stays,
/// because memberships and audit entries point to it. It runs without a tenant.
/// </summary>
public sealed record DeleteUser(string UserId);

// The command sets a state, so the same event applied twice gives the same row (I1), and only the first deletion is announced. A user
// the catalog does not know changes nothing. Audit records hold no name or email address (K3), so the erased copy is all there is to
// erase.
public static class DeleteUserHandler
{
    public static async Task<UserDeleted?> HandleAsync(DeleteUser command, IUserCatalog users, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var first = user.Delete(now);
        await users.SaveChangesAsync(cancellationToken);
        return first ? new UserDeleted(Guid.CreateVersion7(now), now, user.Id) : null;
    }
}
