using SharedKernel;

namespace Billing.Contracts.Tenants;

/// <summary>
/// A system admin ended the tenant's Contract: it pays by card in the app again (B12). Published through the outbox with the tenant's name
/// of that moment, which the Activity log keeps (K3, 12.12, 7.11); the tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="EndedBy">The user id of the system admin.</param>
[MessageName("contract-ended")]
public sealed record ContractEndedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName, Guid EndedBy);
