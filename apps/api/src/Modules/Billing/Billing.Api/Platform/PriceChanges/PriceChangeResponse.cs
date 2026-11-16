namespace Billing.Api.Platform.PriceChanges;

/// <summary>
/// A plan's payers by card and its price change (B13): <c>payers</c> and <c>yearlyPayers</c>, without the ones that end at their period's
/// end (6.14, "Affects {count} organisations"); <c>onOldPrice</c>, the payers on a price the plan no longer has, or null (6.15); and
/// <c>movesAt</c>, the day they move to the new price, or null while nothing is scheduled (6.16).
/// </summary>
public sealed record PriceChangeResponse(int Payers, int YearlyPayers, OldPriceResponse? OnOldPrice, DateTimeOffset? MovesAt);
