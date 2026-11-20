namespace ControlPlane.Api.Tenant.Usage;

/// <summary>A meter's use in a past month, its extra and the extra's price at the month's last use, in minor units.</summary>
public sealed record PastMeterUsageResponse(string Key, long Used, long Extra, long ExtraAmount);
