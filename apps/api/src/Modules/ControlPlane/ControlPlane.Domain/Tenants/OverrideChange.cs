using ControlPlane.Domain.Plans;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.SetOverride"/> did: the override, new or changed, its value before, none for a new one, and whether anything
/// changed: the value, the expiry and the reason it has change nothing (I1).
/// </summary>
internal sealed record OverrideChange(EntitlementOverride Override, OverrideValue? Old, bool Changed);
