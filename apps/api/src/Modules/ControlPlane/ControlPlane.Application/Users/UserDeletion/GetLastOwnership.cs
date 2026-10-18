using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>
/// The check before a user's deletion, in one tenant: whether the user is its last Owner (K1). It runs in that tenant, without a lock:
/// the removal checks again under the tenant's lock.
/// </summary>
public sealed record GetLastOwnership(Guid UserId);

public static class GetLastOwnershipHandler
{
    public static async Task<bool> HandleAsync(GetLastOwnership query, ITenantCatalog catalog, CancellationToken cancellationToken) =>
        await catalog.FindMembershipAsync(query.UserId, cancellationToken) is { } membership
        && !Tenant.CheckUserDeletion(membership, await catalog.CountOwnersAsync(cancellationToken)).IsSuccess;
}
