namespace Notifications.Domain.Notifications;

/// <summary>
/// One category of the catalog (U1, U5): who it can reach, whether it is required, and each channel's default. A category is one switch
/// for each channel: its types cannot be chosen one by one, so a type that needs a switch of its own goes into a category of its own. A
/// required category goes on every channel, whatever the person chose, and the preferences show it locked. Its name and description are
/// texts of <c>packages/emails</c> (U2).
/// </summary>
public sealed record NotificationCategoryKind(
    string Name,
    NotificationAudience Audience,
    bool Required,
    IReadOnlyDictionary<NotificationChannel, bool> Defaults)
{
    /// <summary>Whether the channel is on for a person who has not chosen.</summary>
    public bool DefaultOf(NotificationChannel channel) => Defaults[channel];
}
