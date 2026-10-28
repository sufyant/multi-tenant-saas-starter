using ControlPlane.Domain.Plans;

namespace ControlPlane.Application.Plans;

/// <summary>
/// The plan aggregate for the commands: a plan with its values, and the default mark (Q1). Plans belong to no tenant (R6), so a command reads
/// them in any transaction.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IPlanRepository
{
    /// <summary>The id of the default plan, which a new tenant gets when its creation names no plan. There is always one.</summary>
    internal Task<Guid> FindDefaultPlanIdAsync(CancellationToken cancellationToken);
}
