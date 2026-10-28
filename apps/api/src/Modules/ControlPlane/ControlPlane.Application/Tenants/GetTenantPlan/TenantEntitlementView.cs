using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetTenantPlan;

/// <summary>
/// One key of the tenant's entitlements as the platform reads it: whether it is a feature, the plan's value (none when the plan has none,
/// and the key is then closed), the tenant's override, the effective value at the moment of the read, and what the tenant has that the key
/// counts.
/// </summary>
public sealed record TenantEntitlementView(
    string Key,
    bool IsFeature,
    EntitlementValueView? PlanValue,
    EntitlementOverrideView? Override,
    EntitlementValueView Effective,
    int Count);
