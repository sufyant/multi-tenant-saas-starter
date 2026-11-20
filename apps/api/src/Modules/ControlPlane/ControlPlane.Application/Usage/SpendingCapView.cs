namespace ControlPlane.Application.Usage;

/// <summary>
/// The tenant's spending cap (11.02, 11.04): its amount and the month's extra so far, in minor units excl. VAT, each meter's rounded half
/// away from zero, and the moment the month reached the cap, none before.
/// </summary>
public sealed record SpendingCapView(long Amount, long ExtraAmount, DateTimeOffset? ReachedAt);
