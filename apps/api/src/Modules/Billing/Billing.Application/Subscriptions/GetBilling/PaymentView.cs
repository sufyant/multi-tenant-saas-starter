namespace Billing.Application.Subscriptions.GetBilling;

/// <summary>
/// Where a failed payment of the tenant's subscription stands (B11; 10.15 to 10.17): its state's name, a <c>PaymentState</c> (N7r), when the
/// first attempt failed, how many failed, the provider's next attempt, the end of billing's grace and the move to the default plan, the
/// amount due without tax in the currency's minor unit, and the card the provider tried.
/// </summary>
public sealed record PaymentView(
    string State,
    DateTimeOffset FailedAt,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? GraceEndsAt,
    DateTimeOffset? MovedAt,
    long AmountDue,
    string Currency,
    string? CardBrand,
    string? CardLast4);
