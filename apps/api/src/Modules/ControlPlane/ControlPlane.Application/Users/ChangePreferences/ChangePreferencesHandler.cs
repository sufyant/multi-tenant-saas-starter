using ControlPlane.Contracts.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.ChangePreferences;

// The command sets a state (I1): the values the user has answer as they are, and nothing is written or announced. It locks the user
// row, so two changes of the user run one at a time (K6), and a deletion that commits while it waits is refused (A9). Rejections come
// first (W7).
public static class ChangePreferencesHandler
{
    public static async Task<(Result<PreferencesView>, UserPreferencesChangedEvent?)> HandleAsync(
        ChangePreferencesCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // A person becomes a user only through an invitation or as the first system admin, as GetMe answers.
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var changed = user.ChangePreferences(command.Language, command.TimeZone);
        if (!changed.IsSuccess || changed.Value == StateChange.Unchanged)
        {
            return (changed.Map(_ => new PreferencesView(user.Language, user.TimeZone)), null);
        }

        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (new PreferencesView(user.Language, user.TimeZone), new UserPreferencesChangedEvent(Guid.CreateVersion7(now), now, user.Id));
    }
}
