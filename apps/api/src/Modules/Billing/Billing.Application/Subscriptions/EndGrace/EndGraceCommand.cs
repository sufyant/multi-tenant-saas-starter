using SharedKernel;

namespace Billing.Application.Subscriptions.EndGrace;

/// <summary>
/// Ends billing's grace of an unpaid subscription at <paramref name="GraceEndsAt"/> (B11). Scheduled when the grace starts; it travels in the
/// tenant's transaction, which its envelope declares (W3).
/// </summary>
[MessageName("end-grace")]
public sealed record EndGraceCommand(Guid TenantId, DateTimeOffset GraceEndsAt);
