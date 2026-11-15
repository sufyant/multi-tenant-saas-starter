namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The card Stripe tried: its brand, as Stripe names it (<c>visa</c>, <c>mastercard</c>), and its last four digits.</summary>
public sealed record PaymentCardResponse(string Brand, string Last4);
