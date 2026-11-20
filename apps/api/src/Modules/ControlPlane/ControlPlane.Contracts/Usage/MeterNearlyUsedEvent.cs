using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A use first took the tenant's count of a meter to 80% of its plan's quota this month (step 40c; 13.22; E10). A use of a meter is a count,
/// not a person's change, so no command records it and the Activity log has no entry of it. Published through the outbox once for each
/// meter and month. The tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="MeterKey">The code's key of the meter.</param>
/// <param name="Used">The count after the use.</param>
/// <param name="UnitPrice">The price of the extra over the quota, in minor units for each <paramref name="UnitSize"/> units; none for a meter that stops at its quota.</param>
/// <param name="UnitSize">The units that <paramref name="UnitPrice"/> pays for; none with it.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
/// <param name="ResetsOn">The first day of the next month of UTC, when the count starts again.</param>
[MessageName("meter-nearly-used")]
public sealed record MeterNearlyUsedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string MeterKey,
    long Used,
    long Quota,
    long? UnitPrice,
    long? UnitSize,
    string Currency,
    DateOnly ResetsOn);
