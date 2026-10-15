using SharedKernel;

namespace ControlPlane.Contracts;

/// <summary>
/// The catalog lookup tenant resolution needs before any tenant is known: the host asks it on every tenant route (T7).
/// </summary>
public interface ITenantDirectory
{
    /// <summary>
    /// The user's membership in the tenant with this id, when the tenant is active and the user is its member. A member of a
    /// suspended tenant is refused with a reason. Null when the user is not a member, or the tenant is neither active nor suspended, so
    /// a stranger cannot tell a suspended tenant from one that does not exist (T3).
    /// </summary>
    Task<Result<TenantMembership>?> FindMembershipAsync(Guid tenantId, string externalUserId, CancellationToken cancellationToken);
}

/// <summary>A verified membership: the tenant and the permissions the member's role holds there.</summary>
public sealed record TenantMembership(Guid TenantId, IReadOnlySet<string> Permissions);
