namespace ControlPlane.Domain.Plans;

/// <summary>
/// One key a change would put a tenant over: what the tenant has (<see cref="EntitlementUsage.CountOf"/>), the new value, and how far
/// over it goes: the count less a limit, or the whole count for a feature that turns off while the tenant uses it.
/// </summary>
internal sealed record Excess(string Key, int Count, EntitlementValue NewValue, int Amount);
