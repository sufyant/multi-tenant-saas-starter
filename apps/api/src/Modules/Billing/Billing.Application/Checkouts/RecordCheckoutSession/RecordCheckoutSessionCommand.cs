using SharedKernel;

namespace Billing.Application.Checkouts.RecordCheckoutSession;

/// <summary>Keeps the provider's page of the tenant's checkout of this key, in the tenant of the route (B7).</summary>
[MessageName("record-checkout-session")]
public sealed record RecordCheckoutSessionCommand(Guid Key, string SessionId, Uri Url, DateTimeOffset ExpiresAt);
