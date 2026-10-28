namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// One key's value in a request: a feature's state in <c>enabled</c>, or a limit's number of 0 or more in <c>maximum</c>, or an unlimited
/// limit with <c>unlimited</c> true. Exactly one, of the key's kind; else 400 <c>plan.entitlement_invalid</c> with the key. Unlimited is a
/// value of its own, never a number.
/// </summary>
public sealed record EntitlementValueRequest(bool? Enabled = null, int? Maximum = null, bool Unlimited = false);
