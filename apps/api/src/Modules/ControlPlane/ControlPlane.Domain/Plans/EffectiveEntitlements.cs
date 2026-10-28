using System.Collections.Frozen;
using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// The value of every key of the code's list for one tenant at one moment: the plan's value, overlaid by the tenant's override while it
/// has not expired. A key that the plan has no value for, and no override holds, is closed: a feature is off and a limit is 0. It is
/// computed at each read from the stored values and expiries, with the moment of the read, so an override stops at its expiry, with no
/// wait for a cache.
/// </summary>
internal sealed class EffectiveEntitlements
{
    private readonly FrozenDictionary<string, EntitlementValue> _values;

    private EffectiveEntitlements(FrozenDictionary<string, EntitlementValue> values) => _values = values;

    /// <summary>Every key of the code's list with its effective value.</summary>
    public IReadOnlyDictionary<string, EntitlementValue> Values => _values;

    public static EffectiveEntitlements Of(IReadOnlyDictionary<string, EntitlementValue> plan, IEnumerable<OverrideValue> overrides, DateTimeOffset now)
    {
        var active = overrides.Where(entry => entry.IsActiveAt(now)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return new(EntitlementKeys.All.ToFrozenDictionary(
            key => key.Key,
            key => active.GetValueOrDefault(key.Key) ?? plan.GetValueOrDefault(key.Key) ?? EntitlementValue.ClosedOf(key.Value),
            StringComparer.Ordinal));
    }

    public EntitlementValue ValueOf(string key) =>
        _values.TryGetValue(key, out var value) ? value : throw new ArgumentException("The key is not on the code's list.", nameof(key));

    /// <summary>Whether the feature is on; 403 <c>plan.feature_unavailable</c>, with the key, when it is off.</summary>
    public Result CheckFeature(string feature) =>
        ValueOf(feature).IsOn
            ? Result.Success()
            : Error.Forbidden(ControlPlaneErrorCodes.PlanFeatureUnavailable, "The tenant's plan does not include this feature.") with { Entitlement = feature };

    /// <summary>
    /// Whether the tenant may add one more thing the limit counts, at this count; 409 <c>plan.limit_reached</c>, with the key, when the
    /// count is at the limit or over it. Deleting and reducing never ask.
    /// </summary>
    public Result CheckLimit(string limit, int count) =>
        ValueOf(limit).Allows(count)
            ? Result.Success()
            : Error.Conflict(ControlPlaneErrorCodes.PlanLimitReached, "The tenant has as many as its plan allows.") with { Entitlement = limit };
}
