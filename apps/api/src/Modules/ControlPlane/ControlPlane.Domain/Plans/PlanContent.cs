namespace ControlPlane.Domain.Plans;

/// <summary>
/// What a plan gives and costs, as a create or an edit sends it whole: its value of some keys, its prices, and its setting of some meters.
/// A key or a meter left out is not on the plan.
/// </summary>
internal sealed record PlanContent(
    IReadOnlyDictionary<string, EntitlementValue> Values,
    PlanPrices Prices,
    IReadOnlyDictionary<string, MeterSetting> Meters);
