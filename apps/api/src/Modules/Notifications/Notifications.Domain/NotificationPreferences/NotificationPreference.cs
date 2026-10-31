using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Domain.NotificationPreferences;

/// <summary>
/// A user's notification preferences (U5), the same in every tenant: a table whose rows are the categories and whose columns are the
/// channels. Each channel has a main switch, "All", and each category one switch for each channel; its types cannot be chosen one by one.
/// Only the choices that differ from the defaults are stored, so a user who never chose has no row. A choice stays when its category
/// stops reaching the user, and applies again when it reaches them again.
/// </summary>
internal sealed class NotificationPreference
{
    // For EF Core, which reads the preferences back.
    private NotificationPreference()
    {
        Channels = null!;
        Categories = null!;
    }

    private NotificationPreference(
        Guid userId, Dictionary<NotificationChannel, bool> channels, Dictionary<string, Dictionary<NotificationChannel, bool>> categories) =>
        (UserId, Channels, Categories) = (userId, channels, categories);

    public Guid UserId { get; private init; }

    /// <summary>The main switch of each channel that is off. On is the default, so an "All" that is on is not kept.</summary>
    public Dictionary<NotificationChannel, bool> Channels { get; private set; }

    /// <summary>The choices that differ from their category's default, by category and channel. A required category has none.</summary>
    public Dictionary<string, Dictionary<NotificationChannel, bool>> Categories { get; private set; }

    /// <summary>Whether every choice is the default, so no row is needed.</summary>
    public bool IsDefault => Channels.Count == 0 && Categories.Count == 0;

    /// <summary>The preferences of a user who never chose.</summary>
    public static NotificationPreference DefaultFor(Guid userId) => new(userId, [], []);

    /// <summary>
    /// Whether a notification of this type goes on the channel (U4): always when its category is required; else when the channel's main
    /// switch is on and the user's choice of the category, or without one the catalog's default, is on.
    /// </summary>
    public bool Sends(NotificationKind kind, NotificationChannel channel) =>
        NotificationCatalog.CategoryOf(kind.Category).Required || (IsOn(channel) && IsOn(kind.Category, channel));

    /// <summary>Whether the channel's main switch is on.</summary>
    public bool IsOn(NotificationChannel channel) => !Channels.TryGetValue(channel, out var on) || on;

    /// <summary>Whether the category is on in the channel, as the screen shows it: always for a required category.</summary>
    public bool IsOn(string category, NotificationChannel channel)
    {
        var kind = NotificationCatalog.CategoryOf(category);
        return kind.Required
            || (Categories.TryGetValue(category, out var chosen) && chosen.TryGetValue(channel, out var on) ? on : kind.DefaultOf(channel));
    }

    /// <summary>
    /// The user's new choices: some main switches, and some categories on some channels. A choice left out keeps its value, and a choice
    /// equal to its default is not kept. A category the catalog does not know, or a required one, is a mistake of ours: the handler refuses
    /// a required category first (W7).
    /// </summary>
    public StateChange Change(IReadOnlyDictionary<NotificationChannel, bool> channels, IReadOnlyList<NotificationChoice> choices)
    {
        if (choices.FirstOrDefault(choice => NotificationCatalog.CategoryOf(choice.Category).Required) is { } locked)
        {
            throw new InvalidOperationException($"The notification category {locked.Category} is required.");
        }

        var keptChannels = Channels
            .Where(choice => !channels.ContainsKey(choice.Key))
            .Concat(channels)
            .Where(choice => !choice.Value)
            .ToDictionary(choice => choice.Key, choice => choice.Value);
        var keptCategories = Categories
            .SelectMany(category => category.Value.Select(channel => new NotificationChoice(category.Key, channel.Key, channel.Value)))
            .Where(old => !choices.Any(choice => choice.Category == old.Category && choice.Channel == old.Channel))
            .Concat(choices)
            .Where(choice => choice.Enabled != NotificationCatalog.CategoryOf(choice.Category).DefaultOf(choice.Channel))
            .GroupBy(choice => choice.Category, StringComparer.Ordinal)
            .ToDictionary(
                category => category.Key,
                category => category.ToDictionary(choice => choice.Channel, choice => choice.Enabled),
                StringComparer.Ordinal);
        if (Same(keptChannels, Channels) && keptCategories.Count == Categories.Count
            && keptCategories.All(category => Categories.TryGetValue(category.Key, out var old) && Same(category.Value, old)))
        {
            return StateChange.Unchanged;
        }

        (Channels, Categories) = (keptChannels, keptCategories);
        return StateChange.Changed;
    }

    private static bool Same(Dictionary<NotificationChannel, bool> left, Dictionary<NotificationChannel, bool> right) =>
        left.Count == right.Count && left.All(choice => right.TryGetValue(choice.Key, out var old) && old == choice.Value);
}
