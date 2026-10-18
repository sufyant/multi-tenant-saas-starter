namespace ControlPlane.Application.Tenants.GetMyTenant;

/// <summary>
/// The tenant as its member sees it, when it was created, and the member's role and the names of its permissions, in ordinal order.
/// The web shows an action only when the list holds its permission; every endpoint still checks the permission itself (A1).
/// </summary>
public sealed record MyTenantDetailView(MyTenantView Tenant, IReadOnlyList<string> Permissions, DateTimeOffset CreatedAt);
