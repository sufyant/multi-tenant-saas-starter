namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// A plan's setting of one meter as an entry keeps it: its key, the quota of each period, <c>overage</c> <c>stop</c> or <c>charge</c>, and
/// the price of the extra, <c>unitPrice</c> minor units for each <c>unitSize</c> units (step 40).
/// </summary>
public sealed record AuditMeterSettingResponse(string Key, long Quota, AuditMeterSettingResponseOverage Overage, long UnitPrice, long UnitSize);
