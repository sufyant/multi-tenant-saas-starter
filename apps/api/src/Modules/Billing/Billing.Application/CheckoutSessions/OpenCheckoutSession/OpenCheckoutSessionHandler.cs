using Billing.Application.Provider;
using Billing.Domain.Subscriptions;

namespace Billing.Application.CheckoutSessions.OpenCheckoutSession;

// The handler takes no DbContext, so no transaction is open during the provider's call (section 3, "Handlers and pipeline").
public static class OpenCheckoutSessionHandler
{
    public static Task<Uri> HandleAsync(OpenCheckoutSessionQuery query, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.OpenCheckoutAsync(
            new CheckoutOrder(
                query.TenantId, query.PaidBy, query.CustomerId ?? throw new InvalidOperationException("The endpoint names the customer before the checkout."), query.PlanId, query.PlanName, query.Currency,
                Enum.Parse<BillingInterval>(query.Interval) == BillingInterval.Yearly, query.BaseAmount, query.PerPersonAmount, query.People, query.SuccessUrl, query.CancelUrl),
            cancellationToken);
}
