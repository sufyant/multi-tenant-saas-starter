using SharedKernel;

namespace Billing.Application.Checkouts.RecordCheckoutSession;

// A page of a key that another checkout replaced since changes nothing: the reservation that replaced it expired it, or will.
public static class RecordCheckoutSessionHandler
{
    public static async Task HandleAsync(RecordCheckoutSessionCommand command, ICheckoutRepository checkouts, CancellationToken cancellationToken)
    {
        if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout
            && checkout.Record(command.Key, command.SessionId, command.Url.ToString(), command.ExpiresAt) == StateChange.Changed)
        {
            await checkouts.SaveChangesAsync(cancellationToken);
        }
    }
}
