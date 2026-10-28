using SharedKernel;

namespace ControlPlane.Application.Plans.CreatePlan;

/// <summary>
/// A system admin creates a plan with a name and a value for some keys; a key left out is closed. Marked default, it takes the mark from
/// the previous default plan in the same transaction.
/// </summary>
[MessageName("create-plan")]
public sealed record CreatePlanCommand(string ActorId, string Name, bool IsDefault, IReadOnlyDictionary<string, EntitlementValueView> Entitlements);
