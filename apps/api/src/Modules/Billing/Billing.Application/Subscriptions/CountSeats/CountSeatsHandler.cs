using Billing.Application.Subscriptions.SetSeats;
using ControlPlane.Contracts.Members;
using Wolverine;

namespace Billing.Application.Subscriptions.CountSeats;

// The count is read at this moment, so the last count wins: two changes at once may reach the provider in another order, and the provider's
// event of the older one brings a count that differs again, which this handler sets once more (ApplySubscription). Row level security keeps
// the count to the tenant of the envelope (R1).
public static class CountSeatsHandler
{
    public static async Task<OutgoingMessages> HandleAsync(CountSeatsCommand command, IMemberDirectory members, CancellationToken cancellationToken)
    {
        var seated = await members.CountSeatedMembersAsync(cancellationToken);
        return seated == command.People ? [] : [new SetSeatsCommand(command.SubscriptionId, seated)];
    }
}
