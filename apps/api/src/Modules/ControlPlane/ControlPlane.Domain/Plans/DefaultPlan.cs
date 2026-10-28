using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// The default mark: the one plan a new tenant gets when its creation names none. It is one row of <c>default_plans</c>, whose key can
/// only be true, so the database holds exactly one default plan, never none and never two.
/// </summary>
internal sealed class DefaultPlan(Guid planId)
{
    /// <summary>The key of the one row.</summary>
    public bool Id { get; private init; } = true;

    public Guid PlanId { get; private set; } = planId;

    /// <summary>
    /// Sets whether the plan is the default. Marking a plan moves the mark from the previous default plan, in the same row and so in the
    /// same transaction. The default plan keeps its mark: another plan takes it (409 <c>plan.default_required</c>). The mark the plan has
    /// changes nothing (I1).
    /// </summary>
    public Result<StateChange> Mark(Guid planId, bool isDefault)
    {
        if (isDefault == (PlanId == planId))
        {
            return StateChange.Unchanged;
        }

        if (!isDefault)
        {
            return Error.Conflict(ControlPlaneErrorCodes.PlanDefaultRequired, "The default plan keeps its mark until another plan takes it.");
        }

        PlanId = planId;
        return StateChange.Changed;
    }
}
