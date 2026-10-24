using ControlPlane.Application.Roles;
using SharedKernel;

namespace ControlPlane.Application.Tenants.GetMyTenant;

// The door read the membership before the handler's transaction began, and a removal may have committed since: to a non-member the
// tenant does not exist (T3). The most custom roles the tenant may have is the setting CreateRole enforces.
public static class GetMyTenantHandler
{
    public static async Task<Result<MyTenantDetailView>> HandleAsync(
        GetMyTenantQuery query,
        ITenantQueries tenants,
        RoleOptions roles,
        CancellationToken cancellationToken) =>
        await tenants.FindMyTenantAsync(query.ActorId, roles.MaxCustomRolesPerTenant, cancellationToken) is { } tenant ? tenant : Errors.TenantNotFound;
}
