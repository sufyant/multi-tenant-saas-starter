using Billing.Application.Contracts;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.CountSeats;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Wolverine;

namespace Billing.Application.Members.CountSeatsAtMemberChange;

// Step 40, billing per person: billing starts when a person accepts an invitation, and the next invoice prorates joins and removals. Each
// change of who takes a seat (A2) counts the seats again for the tenant's subscription that has not ended: a join, a removal, a leave, a
// role change, an Owner made by the platform, and a user's deletion. In the tenant's transaction, which the event's envelope declares (W3).
// A tenant that never paid by card has no subscription, and nothing to count.
public static class CountSeatsAtMemberChangeHandler
{
    public static Task<OutgoingMessages> HandleAsync(MemberJoinedEvent joined, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(MemberRemovedEvent removed, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(MemberLeftEvent left, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(MemberRoleChangedEvent changed, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(OwnerMadeEvent made, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(UserMembershipRemovedEvent removed, ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        CountAsync(subscriptions, contracts, cancellationToken);

    // B12: a Contract tenant pays no card for its seats.
    private static async Task<OutgoingMessages> CountAsync(ISubscriptionQueries subscriptions, IContractRepository contracts, CancellationToken cancellationToken) =>
        !await contracts.IsContractAsync(cancellationToken)
        && await subscriptions.FindSubscriptionAsync(cancellationToken) is { } subscription
        && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status))
            ? [new CountSeatsCommand(subscription.SubscriptionId, null)]
            : [];
}
