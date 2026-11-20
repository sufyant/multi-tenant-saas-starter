using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A use of a charge meter reached the tenant's spending cap this month (step 40c; 13.24; E9, E10): it landed on the cap or was stopped
/// over it, and every later extra of the month stops. Published through the outbox once for each month and cap.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="MeterKey">The code's key of the meter whose use reached the cap.</param>
/// <param name="Cap">The cap, in minor units excl. VAT.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
/// <param name="ResetsOn">The first day of the next month of UTC, when the extra starts again from nothing.</param>
[MessageName("spending-cap-reached")]
public sealed record SpendingCapReachedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string MeterKey,
    long Cap,
    string Currency,
    DateOnly ResetsOn);
