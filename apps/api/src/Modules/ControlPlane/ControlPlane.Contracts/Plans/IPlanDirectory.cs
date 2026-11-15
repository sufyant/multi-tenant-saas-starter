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
}
