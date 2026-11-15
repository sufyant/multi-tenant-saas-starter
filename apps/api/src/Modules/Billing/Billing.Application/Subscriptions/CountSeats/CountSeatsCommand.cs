using SharedKernel;

namespace Billing.Application.Subscriptions.CountSeats;

/// <summary>
/// Counts the tenant's members who take a seat, through ControlPlane's contracts, on ControlPlane's DbContext, as one DbContext owns a
/// handler's transaction (W1), and sets the subscription's fee per person to them when the count differs (step 40, billing per person). It
/// travels in the tenant's transaction, which its envelope declares (W3).
/// </summary>
/// <param name="People">The count that the provider's state holds; none when it is not known, after a member's change, so the provider
/// compares it.</param>
[MessageName("count-seats")]
public sealed record CountSeatsCommand(string SubscriptionId, int? People);
