namespace Billing.Domain.Subscriptions;

/// <summary>
/// The subscription's open invoice whose payment failed, as the provider holds it (B11): the amount due without tax, in the currency's minor
/// unit, when the first attempt failed, how many attempts failed, and when the provider tries again; none when its retries are over. The
/// card it tried names its brand and its last four digits, for the person's banner (10.15, 10.16).
/// </summary>
internal sealed record FailedPayment(
    string InvoiceId,
    long AmountDue,
    string Currency,
    DateTimeOffset FailedAt,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    string? CardBrand,
    string? CardLast4);
