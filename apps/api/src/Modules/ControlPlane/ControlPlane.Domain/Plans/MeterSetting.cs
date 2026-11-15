namespace ControlPlane.Domain.Plans;

/// <summary>A plan's setting of one meter, as a value: its quota, what happens at the quota, and the price of the extra.</summary>
internal sealed record MeterSetting(long Quota, MeterOverage Overage, long UnitPrice, long UnitSize);
