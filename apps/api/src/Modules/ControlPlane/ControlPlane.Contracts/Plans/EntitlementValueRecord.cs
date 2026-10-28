namespace ControlPlane.Contracts.Plans;

/// <summary>
/// One key's value in an event: a feature's state in <paramref name="Enabled"/>, or a limit's number in <paramref name="Maximum"/>, or an
/// unlimited limit, with <paramref name="Unlimited"/> true.
/// </summary>
public sealed record EntitlementValueRecord(bool? Enabled, int? Maximum, bool Unlimited);
