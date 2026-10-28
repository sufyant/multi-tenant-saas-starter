using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A system admin created a tenant, which is active from its creation. Published through the outbox in the creation's transaction; a
/// subscriber handles it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="CreatedBy">The user id of the system admin who created the tenant.</param>
/// <param name="PlanId">The plan the tenant got: the one its creation named, else the default plan. None in an event of the version before
/// step 32, which the new fields follow so that both versions read each other's events.</param>
/// <param name="PlanName">The plan's name at that moment.</param>
[MessageName("tenant-created")]
public sealed record TenantCreatedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string Name, Guid CreatedBy, Guid? PlanId = null, string? PlanName = null);
