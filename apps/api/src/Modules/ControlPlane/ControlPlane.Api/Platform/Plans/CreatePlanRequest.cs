namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A new plan: its name, whether it takes the default mark, and its value of some keys of the list; a key left out has no value, and is
/// closed. Its prices, 0 when left out, and its meters, none when left out (step 40). The API trims the name before it counts its length.
/// </summary>
public sealed record CreatePlanRequest(
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueRequest> Entitlements,
    PlanPricesBody? Prices = null,
    IReadOnlyList<MeterSettingBody>? Meters = null);
