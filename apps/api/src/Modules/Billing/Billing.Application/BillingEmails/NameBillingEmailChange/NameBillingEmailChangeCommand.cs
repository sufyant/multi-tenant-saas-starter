using SharedKernel;

namespace Billing.Application.BillingEmails.NameBillingEmailChange;

/// <summary>
/// The second step of a billing email's entry (B14): it names the tenant through ControlPlane's contracts, on ControlPlane's DbContext, as one
/// DbContext owns a handler's transaction (W1). <c>SetBillingEmailHandler</c> sends it with the moment and the event's id, so a copy of it
/// publishes the same event (I1).
/// </summary>
[MessageName("name-billing-email-change")]
public sealed record NameBillingEmailChangeCommand(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid ChangedBy, bool Removed);
