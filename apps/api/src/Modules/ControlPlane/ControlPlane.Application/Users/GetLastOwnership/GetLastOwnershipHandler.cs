using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Users.GetLastOwnership;

public static class GetLastOwnershipHandler
{
    public static async Task<bool> HandleAsync(GetLastOwnershipQuery query, ITenantRepository tenants, CancellationToken cancellationToken) =>
        !Tenant.CheckUserDeletion(await tenants.FindMemberFactsAsync(query.UserId, cancellationToken)).IsSuccess;
}
