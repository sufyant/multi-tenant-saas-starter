using Notifications.Contracts.NotificationPreferences;
using Notifications.Domain;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;
using SharedKernel;
using Wolverine;

namespace Notifications.Application.NotificationPreferences.ChangeNotificationPreferences;

// A choice of a required category is refused before any change (W7): it is on in every channel and cannot be turned off (U4). A choice
// for a category that cannot reach the user, or that the catalog does not know, is not kept: the screen never offers it. Only the choices
// that differ from the defaults are stored. A change is audited, as a user's other preferences are (K3); the same choices again change and
// announce nothing (N13).
public static class ChangeNotificationPreferencesHandler
{
    public static async Task<(Result<NotificationPreferencesView>, OutgoingMessages)> HandleAsync(
        ChangeNotificationPreferencesCommand command,
        INotificationPreferenceRepository preferences,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (command.Categories.Any(choice => NotificationCatalog.CategoryKinds.Any(category => category.Required && category.Name == choice.Category)))
        {
            return (Errors.NotificationCategoryRequired, []);
        }

        await preferences.DeclareUserAsync(command.UserId, cancellationToken);
        var preference = await preferences.FindNotificationPreferenceForUpdateAsync(command.UserId, cancellationToken);
        var changed = preference.Change(
            command.Channels.ToDictionary(choice => Enum.Parse<NotificationChannel>(choice.Key), choice => choice.Value),
            [
                .. command.Categories
                    .Where(choice => command.Reachable.Contains(choice.Category))
                    .Select(choice => new NotificationChoice(choice.Category, Enum.Parse<NotificationChannel>(choice.Channel), choice.Enabled)),
            ]);
        var view = NotificationPreferencesView.Of(preference, command.Reachable, command.Language);
        if (changed == StateChange.Unchanged)
        {
            return (view, []);
        }

        await preferences.SaveAsync(preference, cancellationToken);
        var now = time.GetUtcNow();
        return (view, [new EmailPreferencesChangedEvent(Guid.CreateVersion7(now), now, command.UserId)]);
    }
}
