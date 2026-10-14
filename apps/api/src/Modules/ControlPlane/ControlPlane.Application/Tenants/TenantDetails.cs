using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Tenants;

/// <summary>A tenant as the system admin sees it: where its creation stands, or whether it is suspended.</summary>
public sealed record TenantDetails(Guid Id, string Name, string Slug, string Status)
{
    internal static TenantDetails Of(Tenant tenant) => new(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString());
}
