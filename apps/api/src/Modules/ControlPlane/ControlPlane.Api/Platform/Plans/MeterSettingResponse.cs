namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan's setting of one meter of the code: its key, the quota of each period, what happens at the quota, and the price of the extra,
/// <c>unitPrice</c> minor units for each <c>unitSize</c> units (step 40).
/// </summary>
public sealed record MeterSettingResponse(string Key, long Quota, MeterSettingResponseOverage Overage, long UnitPrice, long UnitSize);
