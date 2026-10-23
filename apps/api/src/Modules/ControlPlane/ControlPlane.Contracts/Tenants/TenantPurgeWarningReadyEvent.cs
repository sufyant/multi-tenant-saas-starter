using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// One Owner of a closed tenant is to be warned that the tenant will be deleted with its data at the moment of the purge, in the Owner's
/// own language (section 6, "Tenant purge"). Notifications writes the email; it is this message's one handler, so its retries may be
/// scheduled (W5). Published through the outbox, in the tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="OccurredAt">When the warning was sent; the time left until the purge counts from it.</param>
/// <param name="UserId">The Owner, whose warning of this purge the email's idempotency key names.</param>
/// <param name="PurgesAt">When the purge starts: the closure plus the reopen period.</param>
[MessageName("tenant-purge-warning-ready")]
public sealed record TenantPurgeWarningReadyEvent(
    Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid UserId, string Email, string TenantName, Language Language, DateTimeOffset PurgesAt);
