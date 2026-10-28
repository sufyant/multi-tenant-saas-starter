using ControlPlane.Domain.Plans;

namespace ControlPlane.Application.Plans;

/// <summary>
/// One key a change would put a tenant over: what the tenant has that the key counts, the new effective value, and how far over it goes.
/// For a feature that turns off while the tenant uses it, the count is what uses it, and the amount is the whole count.
/// </summary>
public sealed record ExcessView(string Key, int Count, EntitlementValueView NewValue, int Amount)
{
    internal static ExcessView Of(Excess excess) => new(excess.Key, excess.Count, EntitlementValueView.Of(excess.NewValue), excess.Amount);
}
