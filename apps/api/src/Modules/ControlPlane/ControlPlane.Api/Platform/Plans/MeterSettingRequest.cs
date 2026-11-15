namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan's setting of one meter of the code in a request: its key, the quota of each period, what happens at the quota, and the price of
/// the extra, <c>unitPrice</c> minor units for each <c>unitSize</c> units. A key the code does not define, a key sent twice, a quota or a
/// unit size out of range, or a unit price out of the prices' range answers 400 <c>plan.meter_invalid</c> (step 40). The template defines
/// no meter.
/// </summary>
public sealed record MeterSettingRequest(string Key, long Quota, MeterSettingRequestOverage Overage, long UnitPrice, long UnitSize);
