namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A plan's setting of one meter in an event: the quota of each period, <paramref name="Overage"/> <c>Stop</c> or <c>Charge</c>, and the
/// price of the extra, <paramref name="UnitPrice"/> minor units for each <paramref name="UnitSize"/> units (step 40).
/// </summary>
public sealed record MeterSettingRecord(string Key, long Quota, string Overage, long UnitPrice, long UnitSize);
