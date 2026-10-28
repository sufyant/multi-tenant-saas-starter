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

    /// <summary>
    /// The plan with this id and its values, locked for the rest of the transaction, so two edits of one plan run one after the other
    /// (K6a). None when no plan has the id.
    /// </summary>
    internal Task<Plan?> FindPlanForUpdateAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>The plan with this id as a command names it, its id and its name, read without a lock; none when no plan has the id.</summary>
    internal Task<PlanNames?> FindPlanNameAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>
    /// The default mark, locked for the rest of the transaction, so two plans never take it at once (K6a). The plan row is locked first,
    /// then the mark.
    /// </summary>
    internal Task<DefaultPlan> FindDefaultPlanForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether another plan than the one with this id has the name, compared with <c>lower()</c> as the unique index compares it. The
    /// index is the second guard (K6b).
    /// </summary>
    internal Task<bool> IsNameTakenAsync(string name, Guid? planId, CancellationToken cancellationToken);

    /// <summary>Adds the new plan with its values; the caller saves.</summary>
    internal void Add(Plan plan);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
