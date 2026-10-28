using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetMyTenant;

/// <summary>
/// The tenant as its member sees it, when it was created, and the member's role and the names of its permissions, in ordinal order.
/// The web shows an action only when the list holds its permission; every endpoint still checks the permission itself (A1). It also
/// holds the name of the tenant's plan, the effective value of every key of the list at this moment, and, for each limit, the tenant's
/// count, so the web shows the plan and adds no list up (docs/flows/plans-and-entitlements.md).
/// </summary>
public sealed record MyTenantDetailView(
    MyTenantView Tenant,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt,
    string PlanName,
    IReadOnlyDictionary<string, EntitlementValueView> Entitlements,
    IReadOnlyDictionary<string, int> Usage);
