namespace Billing.Application.PriceChanges.GetPriceChange;

/// <summary>
/// A plan's payers by card (6.14): how many, and how many of them yearly, without the ones that end at their period's end; the payers on a
/// price the plan no longer has, none when nobody is (6.15); and the day the plan's latest change moves them, none while nothing is
/// scheduled (B13).
/// </summary>
public sealed record PriceChangeView(int Payers, int YearlyPayers, OldPriceView? OnOldPrice, DateTimeOffset? MovesAt);
