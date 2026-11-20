using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A month of UTC ended in which the tenant used a charge meter over its quota (step 40c; E11): the month's extra, and what it costs at the
/// price of its last use, rounded half away from zero, as the tenant's Usage shows it. Published through the outbox once for each meter and
/// month with extra; Billing charges it to a tenant that pays by card.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="MeterKey">The code's key of the meter.</param>
/// <param name="Period">The first day of the month.</param>
/// <param name="Extra">The units used over the quota in the month.</param>
/// <param name="Amount">What the extra costs, in minor units of <paramref name="Currency"/>; more than 0.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
[MessageName("meter-month-closed")]
public sealed record MeterMonthClosedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string MeterKey,
    DateOnly Period,
    long Extra,
    long Amount,
    string Currency);
