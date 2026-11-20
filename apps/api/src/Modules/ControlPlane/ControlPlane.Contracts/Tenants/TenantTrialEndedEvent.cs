using ControlPlane.Contracts.Plans;
using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A tenant's trial reached its end, and the tenant returned to the plan it had before the trial, or to the default plan (step 40d). Nobody
/// acted. Published through the outbox; a subscriber handles it in the tenant's transaction, as the tenant's id travels in the message's
/// envelope. It names both plans as they were at that moment.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="RetentionShortened">The Activity log period that got shorter with the plan, none when it did not (K14).</param>
[MessageName("tenant-trial-ended")]
public sealed record TenantTrialEndedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid TrialPlanId,
    string TrialPlanName,
    Guid NewPlanId,
    string NewPlanName,
    AuditRetentionShortening? RetentionShortened = null);
