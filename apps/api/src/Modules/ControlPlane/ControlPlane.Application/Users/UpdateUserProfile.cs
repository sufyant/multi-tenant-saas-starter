using ControlPlane.Application.Ports;

namespace ControlPlane.Application.Users;

/// <summary>
/// Sets the catalog's copy of a user's profile to what the identity provider sent. A profile of null means the identity provider
/// deleted the user: the copy is erased and the user is marked deleted. It runs without a tenant.
/// </summary>
public sealed record UpdateUserProfile(string UserId, UserProfile? Profile);

// The command sets a state, so the same event applied twice gives the same row, and an older event is ignored by its time (I1).
// A user the catalog does not know is not added: a person becomes a catalog user only through an invitation or as the first
// system admin.
public static class UpdateUserProfileHandler
{
    public static async Task HandleAsync(
        UpdateUserProfile command,
        IUserProfiles users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return;
        }

        if (command.Profile is { } profile)
        {
            user.CopyProfile(profile.Email, profile.FirstName, profile.LastName, profile.UpdatedAt);
        }
        else
        {
            user.Delete(time.GetUtcNow());
        }

        await users.SaveChangesAsync(cancellationToken);
    }
}
