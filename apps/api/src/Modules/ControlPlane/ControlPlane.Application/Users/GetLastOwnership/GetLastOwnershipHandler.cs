using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Users.GetLastOwnership;

public static class GetLastOwnershipHandler
{
    public static async Task<bool> HandleAsync(GetLastOwnershipQuery query, ITenantCatalog catalog, CancellationToken cancellationToken) =>
        await catalog.FindMembershipAsync(query.UserId, cancellationToken) is { } membership
        && !Tenant.CheckUserDeletion(membership, await catalog.CountOwnersAsync(cancellationToken)).IsSuccess;
}
