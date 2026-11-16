using SharedKernel;

namespace ControlPlane.Application.Plans.UpdatePlan;

/// <summary>
/// A system admin gives a plan a name, a default mark and exactly these values; a key left out loses its value and is then closed.
/// Prices or meters left out keep the plan's (step 40), so a client of the version before keeps them. The
/// edit applies at once to every tenant on the plan: there are no plan versions. Marked default, the plan takes the mark from the previous
/// default plan in the same transaction; the default plan keeps its mark until another takes it. A change of the prices names who pays
/// the new price, and when; none means new customers only (B13).
/// </summary>
[MessageName("update-plan")]
public sealed record UpdatePlanCommand(
    string ActorId,
    Guid PlanId,
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueView> Entitlements,
    PlanPricesView? Prices = null,
    IReadOnlyList<MeterSettingView>? Meters = null,
    PriceChangeView? PriceChange = null);
