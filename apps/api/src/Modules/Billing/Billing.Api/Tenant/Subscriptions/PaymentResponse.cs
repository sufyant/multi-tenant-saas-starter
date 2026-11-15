namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// A failed payment of the subscription (B11; 10.15 to 10.17): <c>retrying</c> while Stripe tries again at <c>nextAttemptAt</c>,
/// <c>grace</c> once its retries are over and the plan stays until <c>graceEndsAt</c>, and <c>moved_to_default</c> since <c>movedAt</c>, until
/// the open invoice is paid. <c>failedAt</c> is when the first attempt failed and <c>attempts</c> how many failed. <c>amountDue</c> is the open
/// invoice's amount without tax, in the minor unit of <c>currency</c>. <c>card</c> is the card Stripe tried, or null.
/// </summary>
public sealed record PaymentResponse(
    PaymentResponseState State,
    DateTimeOffset FailedAt,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? GraceEndsAt,
    DateTimeOffset? MovedAt,
    long AmountDue,
    string Currency,
    PaymentCardResponse? Card);
