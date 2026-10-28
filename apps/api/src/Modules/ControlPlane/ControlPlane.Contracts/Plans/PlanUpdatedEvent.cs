using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A system admin edited a plan: its name, its default mark or its values. Published only when something changed, without a tenant. The
/// edit applies at once to every tenant on the plan.
/// </summary>
/// <param name="UpdatedBy">The user id of the system admin who edited the plan.</param>
/// <param name="PreviousDefaultPlanId">The plan that had the default mark, when the edit moved the mark to this plan; else none.</param>
[MessageName("plan-updated")]
public sealed record PlanUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PlanId,
    Guid UpdatedBy,
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueRecord> OldEntitlements,
    IReadOnlyDictionary<string, EntitlementValueRecord> NewEntitlements,
    Guid? PreviousDefaultPlanId = null,
    string? PreviousDefaultPlanName = null);
