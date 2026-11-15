namespace Audit.Application.Plans;

/// <summary>A plan's setting of one meter as an entry keeps it: its key, quota, overage (<c>Stop</c> or <c>Charge</c>) and the extra's price.</summary>
public sealed record AuditMeterSetting(string Key, long Quota, string Overage, long UnitPrice, long UnitSize)
{
    internal static IReadOnlyList<AuditMeterSetting>? Of(IReadOnlyList<ControlPlane.Contracts.Plans.MeterSettingRecord>? meters) =>
        meters is null ? null : [.. meters.Select(meter => new AuditMeterSetting(meter.Key, meter.Quota, meter.Overage, meter.UnitPrice, meter.UnitSize))];
}
