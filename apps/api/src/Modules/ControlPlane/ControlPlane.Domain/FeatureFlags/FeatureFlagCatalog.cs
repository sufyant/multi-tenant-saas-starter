using System.Text.RegularExpressions;

namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// Every feature flag the code defines, and the keys of the flags the code removed. On start the API stores a new flag and archives a
/// retired one (docs/flows/feature-flags.md). A retired key stays here for good, so no later flag takes its name: the history and the
/// platform's log name a flag by its key. A stored flag that the code neither defines nor retires is left as it is: a version before it,
/// as after a rollback, does not know it yet.
/// </summary>
internal sealed partial class FeatureFlagCatalog
{
    public FeatureFlagCatalog(IReadOnlyList<FeatureFlagDefinition> flags, IReadOnlyList<string> retired)
    {
        Flags = flags;
        Retired = retired;
    }

    /// <summary>
    /// The template's flags: one real kill switch, which shows the pattern working (the maintainer's decision of 2026-11-14). A product adds
    /// its own here, with its key in <see cref="FeatureFlagKeys"/>.
    /// </summary>
    public static FeatureFlagCatalog Template { get; } = new(
        [new FeatureFlagDefinition(FeatureFlagKeys.EmailsSend, FeatureFlagType.KillSwitch, "Sends the notification emails. Turn it off to stop them in an incident.")],
        []);

    public IReadOnlyList<FeatureFlagDefinition> Flags { get; }

    /// <summary>The keys of the flags the code removed, which no flag may take again.</summary>
    public IReadOnlyList<string> Retired { get; }

    /// <summary>
    /// The mistakes of the catalog, none when it is sound: a key that is not lowercase and dotted, a key defined twice or retired twice, a
    /// retired key that a flag takes, and a flag without a description. A test checks the template's (<c>FeatureFlagCatalogTests</c>).
    /// </summary>
    public IReadOnlyList<string> Mistakes()
    {
        var keys = Flags.Select(flag => flag.Key).Concat(Retired).ToList();
        return
        [
            .. keys.Where(key => !KeyPattern().IsMatch(key)).Select(key => $"{key} is not a lowercase, dotted key."),
            .. Flags.GroupBy(flag => flag.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key} is defined twice."),
            .. Retired.GroupBy(key => key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key} is retired twice."),
            .. Flags.Where(flag => Retired.Contains(flag.Key, StringComparer.Ordinal)).Select(flag => $"{flag.Key} is retired; a removed flag's name is never used again."),
            .. Flags.Where(flag => string.IsNullOrWhiteSpace(flag.Description)).Select(flag => $"{flag.Key} has no description."),
        ];
    }

    /// <summary>The definition of a flag the code defines, or none.</summary>
    public FeatureFlagDefinition? Find(string key) => Flags.FirstOrDefault(flag => flag.Key == key);

    // As a permission's parts: lowercase letters, digits and underscores, joined by dots, at least two parts.
    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$")]
    private static partial Regex KeyPattern();
}
