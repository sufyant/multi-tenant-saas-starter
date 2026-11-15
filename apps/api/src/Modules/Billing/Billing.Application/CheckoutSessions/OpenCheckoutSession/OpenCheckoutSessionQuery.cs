using SharedKernel;

namespace Billing.Application.CheckoutSessions.OpenCheckoutSession;

/// <summary>
/// Opens the provider's page where a member pays for a plan by card (Stripe Checkout). The page works for a short time and our app stores
/// nothing of it: the signed event of the payment brings the subscription. The interval crosses as its name (N7r).
/// </summary>
[MessageName("open-checkout-session")]
public sealed record OpenCheckoutSessionQuery(
    Guid TenantId,
    Guid PaidBy,
    string? Email,
    string? CustomerId,
    Guid PlanId,
    string PlanName,
    string Currency,
    string Interval,
    long BaseAmount,
    long PerPersonAmount,
    int People,
    Uri SuccessUrl,
    Uri CancelUrl);
