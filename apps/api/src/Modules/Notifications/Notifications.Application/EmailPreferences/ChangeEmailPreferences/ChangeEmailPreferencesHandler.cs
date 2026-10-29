using Notifications.Contracts.EmailPreferences;
using SharedKernel;
using Wolverine;

namespace Notifications.Application.EmailPreferences.ChangeEmailPreferences;

// A choice for a category that cannot reach the user, or that the catalog does not know, is not kept: the screen never offers it. Only the
// choices that differ from the defaults are stored. A change is audited, as a user's other preferences are (K3); the same choices again
// change and announce nothing (N13).
public static class ChangeEmailPreferencesHandler
{
    public static async Task<(Result<EmailPreferencesView>, OutgoingMessages)> HandleAsync(
        ChangeEmailPreferencesCommand command,
        IEmailPreferenceRepository preferences,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        await preferences.DeclareUserAsync(command.UserId, cancellationToken);
        var preference = await preferences.FindEmailPreferenceForUpdateAsync(command.UserId, cancellationToken);
        var changed = preference.Change(
            command.Enabled,
            command.Categories.Where(choice => command.Reachable.Contains(choice.Key)).ToDictionary(choice => choice.Key, choice => choice.Value, StringComparer.Ordinal));
        var view = new EmailPreferencesView(
            preference.Enabled, [.. command.Reachable.Select(category => new CategoryEmailPreferenceView(category, preference.IsOn(category)))]);
        if (changed == StateChange.Unchanged)
        {
            return (view, []);
        }

        await preferences.SaveAsync(preference, cancellationToken);
        var now = time.GetUtcNow();
        return (view, [new EmailPreferencesChangedEvent(Guid.CreateVersion7(now), now, command.UserId)]);
    }
}
