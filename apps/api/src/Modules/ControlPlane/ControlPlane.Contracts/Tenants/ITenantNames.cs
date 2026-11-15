namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A tenant's name as another module's record keeps it from that moment, such as billing's events for the Activity log (K3, step 40).
/// Tenants belong to no tenant (R6), so the read takes the tenant's id. Built on the caller's ControlPlane DbContext (W1).
/// </summary>
public interface ITenantNames
{
    /// <summary>The name of the tenant with this id, or none for a tenant that no longer exists.</summary>
    Task<string?> FindNameAsync(Guid tenantId, CancellationToken cancellationToken);
}
