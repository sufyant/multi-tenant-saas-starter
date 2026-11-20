namespace ControlPlane.Api.Tenant.Usage;

/// <summary>
/// The tenant's spending cap (11.02, 11.04): its amount and this month's extra so far, in minor units excl. VAT, and when this month reached
/// it, null before.
/// </summary>
public sealed record SpendingCapResponse(long Amount, long ExtraAmount, DateTimeOffset? ReachedAt);
