using ControlPlane.Contracts.Plans;
using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin gave a tenant a trial of a plan until its end (step 40d; 4.29). The tenant is on that plan now, and returns to its plan
/// before the trial at the end. Published through the outbox; a subscriber handles it in the tenant's transaction, as the tenant's id
/// travels in the message's envelope. It names the plan as it was at that moment.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="GivenBy">The system admin's user id.</param>
/// <param name="RetentionShortened">The Activity log period that got shorter with the trial's plan, none when it did not (K14).</param>
[MessageName("tenant-trial-given")]
public sealed record TenantTrialGivenEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid GivenBy,
    Guid TrialPlanId,
    string TrialPlanName,
    DateTimeOffset EndsAt,
    AuditRetentionShortening? RetentionShortened = null);
