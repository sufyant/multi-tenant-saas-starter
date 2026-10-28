namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// One key's value: a feature's state in <c>enabled</c>, or a limit's number in <c>maximum</c>, or an unlimited limit with
/// <c>unlimited</c> true. The fields of the other kind are null.
/// </summary>
public sealed record EntitlementValueResponse(bool? Enabled, int? Maximum, bool Unlimited);
