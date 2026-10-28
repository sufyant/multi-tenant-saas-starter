namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// One key a change would put a tenant over: what the tenant has that the key counts (<c>count</c>), the new effective value, and how far
/// over it goes (<c>amount</c>): the count less a limit, or the whole count for a feature that turns off while the tenant uses it.
/// </summary>
public sealed record ExcessResponse(string Key, int Count, EntitlementValueResponse NewValue, int Amount);
