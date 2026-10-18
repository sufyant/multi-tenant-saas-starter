using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Users.GetLastOwnership;

public static class GetLastOwnershipHandler
{
    public static async Task<bool> HandleAsync(GetLastOwnershipQuery query, ITenantRepository tenants, CancellationToken cancellationToken) =>
        await tenants.FindMembershipAsync(query.UserId, cancellationToken) is { } membership
        && !Tenant.CheckUserDeletion(membership, await tenants.CountOwnersAsync(cancellationToken)).IsSuccess;
}
