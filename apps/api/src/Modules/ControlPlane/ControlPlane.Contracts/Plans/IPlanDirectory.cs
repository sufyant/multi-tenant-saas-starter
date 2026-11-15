namespace ControlPlane.Contracts.Plans;

/// <summary>
/// The plans as another module reads them, for billing (step 40): a plan's name, whether it is the default plan, and its prices in the
/// deployment's currency, and the plan that a tenant is on. Plans and tenants' plans belong to no tenant (R6), so the reads take the ids.
/// Built on the caller's ControlPlane DbContext (W1).
/// </summary>
public interface IPlanDirectory
{
    /// <summary>The plan with this id, or none.</summary>
    Task<PlanRecord?> FindPlanAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The plan that the tenant with this id is on; a tenant always has one.</summary>
    Task<Guid> FindPlanIdOfTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
