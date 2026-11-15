using SharedKernel;

namespace ControlPlane.Application.Plans.CreatePlan;

/// <summary>
/// A system admin creates a plan with a name, a value for some keys, its prices and its meters; a key left out is closed. Prices left
/// out are 0, and meters left out are none (step 40). Marked default, it takes the mark from
/// the previous default plan in the same transaction.
/// </summary>
[MessageName("create-plan")]
public sealed record CreatePlanCommand(
    string ActorId,
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueView> Entitlements,
    PlanPricesView? Prices = null,
    IReadOnlyList<MeterSettingView>? Meters = null);
