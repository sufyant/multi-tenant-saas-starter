using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>
/// The check before a user's deletion, in one tenant: whether the user is its last Owner (K1). It runs in that tenant, without a lock:
/// the removal checks again under the tenant's lock.
/// </summary>
public sealed record GetSoleOwnership(Guid UserId);

public static class GetSoleOwnershipHandler
{
    public static async Task<bool> HandleAsync(GetSoleOwnership query, ITenantCatalog catalog, CancellationToken cancellationToken) =>
        await catalog.FindMembershipAsync(query.UserId, cancellationToken) is { } membership
        && !Tenant.CheckUserDeletion(membership, await catalog.CountOwnersAsync(cancellationToken)).IsSuccess;
}
