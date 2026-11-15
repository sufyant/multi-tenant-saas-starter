using SharedKernel;

namespace Billing.Application.Subscriptions.GetBilling;

/// <summary>The billing of the tenant of the route: its subscription, when it has paid by card once. RLS keeps the read to that tenant.</summary>
[MessageName("get-billing")]
public sealed record GetBillingQuery;
