namespace ControlPlane.Domain.Plans;

/// <summary>
/// What a change of a tenant's plan values would put the tenant over, before the system admin decides: each key whose new effective
/// value is a limit under the tenant's count, or a feature that turns off while the tenant uses it, and the keys an override still holds,
/// which the change does not touch. A preview never blocks: nothing is ever deleted, and over a limit nothing new is added
/// (docs/flows/plans-and-entitlements.md).
/// </summary>
internal sealed record PlanPreview(IReadOnlyList<Excess> Excesses, IReadOnlyList<string> OverriddenKeys)
{
    /// <summary>How far over the tenant goes on its furthest key; 0 when it goes over none.</summary>
    public int Furthest => Excesses.Count == 0 ? 0 : Excesses.Max(excess => excess.Amount);

    /// <summary>The preview of the plan values, under the tenant's overrides at this moment, for what the tenant has.</summary>
    public static PlanPreview Of(IReadOnlyDictionary<string, EntitlementValue> plan, IReadOnlyList<OverrideValue> overrides, EntitlementUsage usage, DateTimeOffset now)
    {
        var effective = EffectiveEntitlements.Of(plan, overrides, now);
        var excesses = EntitlementKeys.All.Keys
            .Order(StringComparer.Ordinal)
            .Select(key => ExcessOf(key, effective.ValueOf(key), usage.CountOf(key)))
            .OfType<Excess>()
            .ToList();
        var overridden = overrides.Where(entry => entry.IsActiveAt(now)).Select(entry => entry.Key).Order(StringComparer.Ordinal).ToList();
        return new PlanPreview(excesses, overridden);
    }

    private static Excess? ExcessOf(string key, EntitlementValue value, int count) => value.Kind switch
    {
        EntitlementKind.Feature when !value.IsOn && count > 0 => new Excess(key, count, value, count),
        EntitlementKind.Limit when value.ExcessOf(count) is > 0 and var amount => new Excess(key, count, value, amount),
        _ => null,
    };
}
