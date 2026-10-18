using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenant of the route, for one of its members: the app shell and Settings › General. The tenant door has let the member in, so the
/// tenant is active.
/// </summary>
public sealed record GetMyTenantQuery(string ActorId);

/// <summary>
/// The tenant as its member sees it, when it was created, and the member's role and the names of its permissions, in ordinal order.
/// The web shows an action only when the list holds its permission; every endpoint still checks the permission itself (A1).
/// </summary>
public sealed record MyTenantDetailView(MyTenantView Tenant, IReadOnlyList<string> Permissions, DateTimeOffset CreatedAt);

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
