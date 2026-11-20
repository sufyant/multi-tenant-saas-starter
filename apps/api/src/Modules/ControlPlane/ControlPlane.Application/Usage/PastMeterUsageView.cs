namespace ControlPlane.Application.Usage;

/// <summary>The use of one meter in a past month, with its extra at the price of the month's last use.</summary>
public sealed record PastMeterUsageView(string Key, long Used, long Extra, long ExtraAmount);
