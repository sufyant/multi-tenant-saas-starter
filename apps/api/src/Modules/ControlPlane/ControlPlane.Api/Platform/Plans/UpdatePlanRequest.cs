namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan's name, its default mark and exactly these values: a key left out loses its value. The default plan keeps its mark until another
/// plan takes it. Its prices and its meters, exactly these; left out, the plan keeps its own (step 40). The API trims the name before it
/// counts its length. A change of the prices names who pays the new price, and when; left out, only new customers pay it (B13).
/// </summary>
public sealed record UpdatePlanRequest(
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueRequest> Entitlements,
    PlanPricesRequest? Prices = null,
    IReadOnlyList<MeterSettingRequest>? Meters = null,
    PlanPriceChangeRequest? PriceChange = null);
