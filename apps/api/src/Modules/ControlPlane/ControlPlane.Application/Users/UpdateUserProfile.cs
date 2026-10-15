using ControlPlane.Application.Ports;
using ControlPlane.Contracts;

namespace ControlPlane.Application.Users;

/// <summary>
/// Sets the catalog's copy of a user's profile to what the identity provider sent. It runs without a tenant.
/// </summary>
public sealed record UpdateUserProfile(string UserId, UserProfile Profile);

// The command sets a state, so the same event applied twice gives the same row, and an older event is ignored by its time (I1).
// Only a change of the email address or the name is announced, so a repeated delivery, or one with the same profile, writes no
// audit record (K3). A user the catalog does not know is not added: a person becomes a catalog user only through an invitation or
// as the first system admin.
public static class UpdateUserProfileHandler
{
    public static async Task<UserProfileUpdated?> HandleAsync(
        UpdateUserProfile command,
        IUserCatalog users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return null;
        }

        var changed = user.CopyProfile(command.Profile.Email, command.Profile.FirstName, command.Profile.LastName, command.Profile.UpdatedAt);
        await users.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return changed ? new UserProfileUpdated(Guid.CreateVersion7(now), now, user.Id) : null;
    }
}
