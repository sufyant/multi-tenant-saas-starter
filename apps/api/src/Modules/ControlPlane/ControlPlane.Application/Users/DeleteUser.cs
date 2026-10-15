using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Users;

/// <summary>
/// The identity provider deleted the user: the catalog erases its copy of their profile and marks the user deleted. The row stays,
/// because memberships and audit entries point to it. It runs without a tenant.
/// </summary>
public sealed record DeleteUser(string UserId);

// The command sets a state, so the same event applied twice gives the same row (I1). A user the catalog does not know changes nothing.
public static class DeleteUserHandler
{
    public static async Task HandleAsync(DeleteUser command, IUserCatalog users, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return;
        }

        user.Delete(time.GetUtcNow());
        await users.SaveChangesAsync(cancellationToken);
    }
}
