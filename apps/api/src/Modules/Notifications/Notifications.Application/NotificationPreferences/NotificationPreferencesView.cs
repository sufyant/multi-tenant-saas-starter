using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Application.NotificationPreferences;

/// <summary>
/// A user's notification preferences as the screen shows them (U5): the main switch of each channel, and each category that can reach the
/// user, with its types' short names in the reader's language and its switch in each channel. A required category is on in every channel
/// and locked.
/// </summary>
public sealed record NotificationPreferencesView(IReadOnlyList<ChannelPreferenceView> Channels, IReadOnlyList<CategoryPreferenceView> Categories)
{
    internal static NotificationPreferencesView Of(NotificationPreference preference, IReadOnlyList<string> categories, Language language)
    {
        var channels = Enum.GetValues<NotificationChannel>();
        return new(
            [.. channels.Select(channel => new ChannelPreferenceView(channel.ToString(), preference.IsOn(channel)))],
            [
                .. categories.Select(category => new CategoryPreferenceView(
                    category,
                    NotificationCatalog.CategoryOf(category).Required,
                    [.. NotificationCatalog.Kinds.Values.Where(kind => kind.Category == category).Select(kind => TypeOf(kind.Type, language))],
                    [.. channels.Select(channel => new ChannelPreferenceView(channel.ToString(), preference.IsOn(category, channel)))])),
            ]);
    }

    // A type without a short name is a mistake of ours, as a missing text is (U2).
    private static NotificationTypeNameView TypeOf(string type, Language language) =>
        new(type, NotificationText.NameOf(language, type) ?? throw new InvalidOperationException($"The notification texts have no name of {type}."));
}
