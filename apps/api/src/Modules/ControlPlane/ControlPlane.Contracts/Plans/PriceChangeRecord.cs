namespace ControlPlane.Contracts.Plans;

/// <summary>
/// Who pays a plan's new price, and when, in an event (B13): <c>EveryoneAfterNotice</c>, <c>EveryoneFrom</c> with its day, or
/// <c>NewCustomersOnly</c>.
/// </summary>
public sealed record PriceChangeRecord(string Who, DateTimeOffset? From);
