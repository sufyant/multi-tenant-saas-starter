namespace ControlPlane.Application.Plans;

/// <summary>Who pays a plan's new price, and when (B13): the name of the choice (N7r), and the set day of <c>EveryoneFrom</c>.</summary>
public sealed record PriceChangeView(string Who, DateTimeOffset? From);
