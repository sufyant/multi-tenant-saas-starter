using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// The catalog lookup tenant resolution needs before any tenant is known: the host asks it on every tenant route (T7).
/// </summary>
public interface ITenantDirectory
{
    /// <summary>
    /// The user's membership in the tenant with this id, when the tenant is active and the user is its member. A member of a
    /// suspended tenant is refused with <c>tenant.suspended</c>, and a member of a closed one with <c>tenant.closed</c>. Anyone else,
    /// when the user is not a member or the tenant is not active, suspended or closed, is refused with <c>tenant.not_found</c>, the error
    /// a handler answers for a tenant that does not exist, so a stranger cannot tell a suspended or a closed tenant, or any tenant, from
    /// one that does not exist (T3).
    /// </summary>
    Task<Result<TenantMembership>> FindMembershipAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken);
}
