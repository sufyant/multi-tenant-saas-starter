namespace ControlPlane.Contracts.Plans;

/// <summary>
/// The plans as another module reads them, for billing (step 40): a plan's name and its prices in the
/// deployment's currency. Plans belong to no tenant (R6), so the read takes the plan's id.
/// Built on the caller's ControlPlane DbContext (W1).
/// </summary>
public interface IPlanDirectory
{
    /// <summary>The plan with this id, or none.</summary>
    Task<PlanRecord?> FindPlanAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The default plan, which a cancellation moves the tenant to (B9), or none while no plan has the mark.</summary>
    Task<PlanRecord?> FindDefaultPlanAsync(CancellationToken cancellationToken);
}
