namespace Billing.Domain.PriceChanges;

/// <summary>
/// Who pays a plan's new price, and when (6.14): every payer at its first period after 30 days' notice, every payer from a set day with
/// the notice 30 days before, or new customers only, whom Checkout and a plan change already pay at the plan's prices (B2, B13).
/// </summary>
internal enum PriceChangeWho
{
    EveryoneAfterNotice,
    EveryoneFrom,
    NewCustomersOnly,
}
