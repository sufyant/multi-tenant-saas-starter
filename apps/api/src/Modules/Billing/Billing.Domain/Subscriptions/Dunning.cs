namespace Billing.Domain.Subscriptions;

/// <summary>
/// What the provider's new state of a subscription asks of billing's dunning (B11), before the row takes it: a payment that failed for the
/// first time, an entry of the Activity log; and the end of the plan that the subscription paid for.
/// </summary>
internal sealed record Dunning(bool PaymentFailed, PlanEnd? End);
