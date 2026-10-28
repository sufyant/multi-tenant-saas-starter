namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A tenant's effective entitlements at one moment: every feature key with whether it is on, and every limit key with its number, or null
/// for unlimited.
/// </summary>
public sealed record TenantEntitlements(IReadOnlyDictionary<string, bool> Features, IReadOnlyDictionary<string, int?> Limits);
