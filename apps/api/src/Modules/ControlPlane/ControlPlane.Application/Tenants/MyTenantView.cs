using ControlPlane.Application.Members;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A tenant as its members see it: active, or suspended or closed so that they cannot enter it, with the role the user holds there.
/// </summary>
public sealed record MyTenantView(Guid Id, string Name, string Slug, string Status, RoleView Role)
{
    internal static MyTenantView Of(Tenant tenant, Role role) => new(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString(), RoleView.Of(role));
}
