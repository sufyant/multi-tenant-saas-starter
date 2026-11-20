using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin added days to a tenant's running trial: it ends later (step 40d; 4.30). Published through the outbox; a subscriber handles
/// it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ExtendedBy">The system admin's user id.</param>
[MessageName("tenant-trial-extended")]
public sealed record TenantTrialExtendedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ExtendedBy,
    DateTimeOffset OldEndsAt,
    DateTimeOffset NewEndsAt);
