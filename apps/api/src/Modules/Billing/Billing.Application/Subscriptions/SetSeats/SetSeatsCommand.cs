using SharedKernel;

namespace Billing.Application.Subscriptions.SetSeats;

/// <summary>
/// Asks the provider to set the subscription's fee per person to this many seated members (step 40, billing per person). The provider's
/// event of the change then brings the count back, and a count that differs from the members is set again (<c>CountSeatsCommand</c>).
/// </summary>
[MessageName("set-seats")]
public sealed record SetSeatsCommand(string SubscriptionId, int People);
