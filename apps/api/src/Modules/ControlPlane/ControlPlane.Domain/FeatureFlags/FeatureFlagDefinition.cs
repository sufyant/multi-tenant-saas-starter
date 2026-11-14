namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// A feature flag as the code defines it: its key, its type and what it does. The key is lowercase and dotted, its area first, as a
/// permission is (<c>roles.bulk_edit</c>). Its default value follows from its type: a release flag is off, a kill switch on. The default
/// applies when no rule can be read, so a failed read never turns new work on and never stops working behaviour.
/// </summary>
internal sealed record FeatureFlagDefinition(string Key, FeatureFlagType Type, string Description)
{
    public bool DefaultValue => Type == FeatureFlagType.KillSwitch;
}
