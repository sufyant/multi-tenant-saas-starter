namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// One key of a tenant's entitlements: whether it is a feature, the plan's value (null when the plan has none: the key is then closed),
/// the tenant's override (null when it has none), the effective value at the moment of the request, and what the tenant has that the key
/// counts (<c>count</c>).
/// </summary>
public sealed record TenantEntitlementResponse(
    string Key,
    bool IsFeature,
    EntitlementValueResponse? PlanValue,
    EntitlementOverrideResponse? Override,
    EntitlementValueResponse Effective,
    int Count);
