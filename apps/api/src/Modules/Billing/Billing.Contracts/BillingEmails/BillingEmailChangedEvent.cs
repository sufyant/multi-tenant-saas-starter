using SharedKernel;

namespace Billing.Contracts.BillingEmails;

/// <summary>
/// A member who manages billing set, changed or removed the tenant's billing email (B14), which the tenant's Activity log records by that
/// member (12.12). Published through the outbox with the tenant's name of that moment (K3); the address itself stays out, as an invitee's
/// does. The tenant's id travels in the envelope too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Removed">Whether the member removed the address.</param>
[MessageName("billing-email-changed")]
public sealed record BillingEmailChangedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string TenantName, Guid ChangedBy, bool Removed);
