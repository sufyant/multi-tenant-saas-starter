using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A tenant as the system admin sees it: where its creation stands, or whether it is suspended or closed. The suspension's reason and
/// the moment of the closure are the system admin's only: a member never sees them.
/// </summary>
public sealed record TenantView(Guid Id, string Name, string Slug, string Status, string? SuspensionReason, DateTimeOffset? ClosedAt)
{
    internal static TenantView Of(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString(), tenant.SuspensionReason, tenant.ClosedAt);
}
