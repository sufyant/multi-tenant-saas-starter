using SharedKernel;

namespace Billing.Contracts.Tenants;

/// <summary>
/// A system admin marked the tenant Contract: it is billed outside the app (B12). Published through the outbox with the tenant's name of
/// that moment, which the Activity log keeps (K3, 12.12, 7.11); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="StartedBy">The user id of the system admin.</param>
[MessageName("contract-started")]
public sealed record ContractStartedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName, Guid StartedBy);
