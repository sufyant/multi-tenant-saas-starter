using SharedKernel;

namespace ControlPlane.Application.Usage.CloseMeterMonth;

/// <summary>
/// Closes the tenant's month of a meter at the month's end, and tells what its extra costs (E11). Scheduled by the month's first use that
/// costs extra; it travels in the tenant's transaction, which its envelope declares (W3).
/// </summary>
[MessageName("close-meter-month")]
public sealed record CloseMeterMonthCommand(Guid TenantId, string MeterKey, DateOnly Period);
