using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Tenants.GetMyTenant;

// The door read the membership before the handler's transaction began, and a removal may have committed since: to a non-member the
// tenant does not exist (T3).
public static class GetMyTenantHandler
{
    public static async Task<Result<MyTenantDetailView>> HandleAsync(GetMyTenantQuery query, ITenantCatalog catalog, CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantAsync(cancellationToken) is not { } tenant
            || await catalog.FindUserAsync(query.ActorId, cancellationToken) is not { } user
            || await catalog.FindMembershipAsync(user.Id, cancellationToken) is not { } membership)
        {
            return Errors.TenantNotFound;
        }

        var role = BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId);
        return new MyTenantDetailView(MyTenantView.Of(tenant, role), [.. role.Permissions.Order(StringComparer.Ordinal)], tenant.CreatedAt);
    }
}
