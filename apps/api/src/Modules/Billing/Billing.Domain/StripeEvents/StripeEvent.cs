namespace Billing.Domain.StripeEvents;

/// <summary>
/// A signed event of the billing provider that billing received, kept by its id so that each event is handled once (Stripe, "Webhooks",
/// "Handle duplicate events"). It belongs to no tenant until its object is read (R6).
/// </summary>
internal sealed class StripeEvent(string id, string type, DateTimeOffset receivedAt)
{
    public const int IdMaxLength = 255;

    public const int TypeMaxLength = 100;

    public string Id { get; private init; } = id;

    public string Type { get; private init; } = type;

    public DateTimeOffset ReceivedAt { get; private init; } = receivedAt;
}
