namespace Billing.Domain.Checkouts;

/// <summary>
/// What a checkout pays for (the plan, the interval, the seats, the currency and the amounts) and the page to come back to: two requests of
/// the same order share one page, as every call of its key is the same call to the provider (B7).
/// </summary>
internal sealed record CheckoutOrderOf(Guid PlanId, bool Yearly, int People, string ReturnUrl, string Currency, long BaseAmount, long PerPersonAmount);
