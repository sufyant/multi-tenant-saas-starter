using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A use first took the tenant's count of a meter to its plan's quota this month (step 40c; 13.23; E10): a stop meter stops now, a charge
/// meter's next uses are extra. Published through the outbox once for each meter and month, as <see cref="MeterNearlyUsedEvent"/> is.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="MeterKey">The code's key of the meter.</param>
/// <param name="UnitPrice">The price of the extra, in minor units for each <paramref name="UnitSize"/> units; none for a meter that stops at its quota.</param>
/// <param name="UnitSize">The units that <paramref name="UnitPrice"/> pays for; none with it.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
/// <param name="ResetsOn">The first day of the next month of UTC, when the count starts again.</param>
[MessageName("meter-used-up")]
public sealed record MeterUsedUpEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string MeterKey,
    long Quota,
    long? UnitPrice,
    long? UnitSize,
    string Currency,
    DateOnly ResetsOn);
