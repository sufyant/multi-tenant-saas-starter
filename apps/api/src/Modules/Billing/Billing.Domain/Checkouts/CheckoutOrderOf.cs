namespace Billing.Domain.Checkouts;

/// <summary>What a checkout pays for, and the page to come back to: two requests of the same order share one page (B7).</summary>
internal sealed record CheckoutOrderOf(Guid PlanId, bool Yearly, int People, string ReturnUrl);
