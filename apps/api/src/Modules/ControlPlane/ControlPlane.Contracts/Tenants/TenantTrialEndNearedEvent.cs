using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A tenant's trial came within a few days of its end (step 40d; 13.16, 3.19). Nobody acted. Published through the outbox; a subscriber handles it in the
/// tenant's transaction, as the tenant's id travels in the message's envelope. It names both plans as they are at that moment.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="TrialPlanName">The plan of the trial.</param>
/// <param name="NextPlanName">The plan the tenant returns to at the trial's end.</param>
/// <param name="EndsAt">The trial's end.</param>
/// <param name="Days">The number of days until the end.</param>
[MessageName("tenant-trial-end-neared")]
public sealed record TenantTrialEndNearedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    string TrialPlanName,
    string NextPlanName,
    DateTimeOffset EndsAt,
    int Days);
