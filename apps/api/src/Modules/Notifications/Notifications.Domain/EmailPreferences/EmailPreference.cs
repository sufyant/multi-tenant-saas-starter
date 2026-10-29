using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Domain.EmailPreferences;

/// <summary>
/// A user's email preferences (U5): a main switch, and under it one switch for each category. They are the user's, the same in every
/// tenant. Only the choices that differ from the defaults are stored: a user who never chose has no row.
/// </summary>
internal sealed class EmailPreference
{
    // For EF Core, which reads the preferences back.
    private EmailPreference()
    {
        Categories = null!;
    }

    private EmailPreference(Guid userId, bool enabled, Dictionary<string, bool> categories) =>
        (UserId, Enabled, Categories) = (userId, enabled, categories);

    public Guid UserId { get; private init; }

    /// <summary>The main switch: off sends no email of any type that is not required.</summary>
    public bool Enabled { get; private set; }

    /// <summary>The categories whose choice differs from their default, by name.</summary>
    public Dictionary<string, bool> Categories { get; private set; }

    /// <summary>Whether every choice is the default, so no row is needed.</summary>
    public bool IsDefault => Enabled && Categories.Count == 0;

    /// <summary>The preferences of a user who never chose.</summary>
    public static EmailPreference DefaultFor(Guid userId) => new(userId, enabled: true, []);

    /// <summary>
    /// Whether a category sends email when the user has not chosen: when a type of it that the preferences govern sends one by default
    /// (the catalog, U1). A category of required types only has no default to change.
    /// </summary>
    public static bool CategoryDefault(string category) =>
        NotificationCatalog.Kinds.Values.Any(kind => kind.Category == category && !kind.Required && kind.EmailByDefault);

    /// <summary>Whether a notification of this type goes by email too: always for a required type, else as the user chose (U4).</summary>
    public bool SendsEmail(NotificationKind kind) =>
        kind.Required || (Enabled && (Categories.TryGetValue(kind.Category, out var chosen) ? chosen : kind.EmailByDefault));

    /// <summary>Whether the category is on, as the preferences screen shows it.</summary>
    public bool IsOn(string category) => Categories.TryGetValue(category, out var chosen) ? chosen : CategoryDefault(category);

    /// <summary>The user's new choices. A category of the catalog only; a choice equal to its default is not kept.</summary>
    public StateChange Change(bool enabled, IReadOnlyDictionary<string, bool> categories)
    {
        if (categories.Keys.FirstOrDefault(category => !NotificationCatalog.Categories.Contains(category)) is { } unknown)
        {
            throw new InvalidOperationException($"The notification catalog has no category {unknown}.");
        }

        var kept = Categories
            .Where(choice => !categories.ContainsKey(choice.Key))
            .Concat(categories)
            .Where(choice => choice.Value != CategoryDefault(choice.Key))
            .ToDictionary(choice => choice.Key, choice => choice.Value, StringComparer.Ordinal);
        if (enabled == Enabled && kept.Count == Categories.Count && kept.All(choice => Categories.TryGetValue(choice.Key, out var old) && old == choice.Value))
        {
            return StateChange.Unchanged;
        }

        (Enabled, Categories) = (enabled, kept);
        return StateChange.Changed;
    }
}
