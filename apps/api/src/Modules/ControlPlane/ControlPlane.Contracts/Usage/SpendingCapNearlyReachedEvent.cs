using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A use first took the month's extra of the tenant's charge meters to 80% of its spending cap (step 40c; 13.43; E10). Published through the
/// outbox once for each month and cap: a change of the cap decides again.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Cap">The cap, in minor units excl. VAT.</param>
/// <param name="ExtraAmount">The month's extra after the use, in minor units, rounded half away from zero.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
/// <param name="ResetsOn">The first day of the next month of UTC, when the extra starts again from nothing.</param>
[MessageName("spending-cap-nearly-reached")]
public sealed record SpendingCapNearlyReachedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    long Cap,
    long ExtraAmount,
    string Currency,
    DateOnly ResetsOn);
