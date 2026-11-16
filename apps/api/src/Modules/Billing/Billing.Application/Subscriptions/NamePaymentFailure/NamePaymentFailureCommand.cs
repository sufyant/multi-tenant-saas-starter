using SharedKernel;

namespace Billing.Application.Subscriptions.NamePaymentFailure;

/// <summary>
/// The second step of a failed payment's entry (B11): it names the tenant through ControlPlane's contracts, on ControlPlane's DbContext, as
/// one DbContext owns a handler's transaction (W1). <c>ApplySubscriptionHandler</c> sends it in the tenant's transaction, with the moment and
/// the event's id, so a copy of it publishes the same event (I1).
/// </summary>
[MessageName("name-payment-failure")]
public sealed record NamePaymentFailureCommand(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    long AmountDue,
    string Currency,
    DateTimeOffset RetriesUntil,
    DateTimeOffset? FailedAt = null,
    DateTimeOffset? NextAttemptAt = null,
    string? CardBrand = null,
    string? CardLast4 = null,
    bool TenantClosed = false);
