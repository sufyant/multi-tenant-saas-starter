using Billing.Application.BillingEmails;
using Billing.Application.Contracts;
using Billing.Application.PriceChanges.NamePriceChange;
using Billing.Application.PriceChanges.RecordPriceChange;
using Billing.Application.Subscriptions;
using Wolverine;

namespace Billing.Application.PriceChanges.AnnounceTenantPriceChange;

// B13: in the tenant's transaction (W3). A Contract tenant is billed outside the app (B12), and a payer that ends, left the plan, pays the
// new price already, or whose subscription the move would skip is told nothing (PriceMoves). The tenant's name is read on ControlPlane's
// DbContext, by the next message (W1). The event's id comes from the change and the tenant, so every copy of a message that brings the
// notice publishes the same event, and Audit keeps one entry (I1).
public static class AnnounceTenantPriceChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        AnnounceTenantPriceChangeCommand command,
        IPriceChangeStore changes,
        IContractRepository contracts,
        ISubscriptionQueries subscriptions,
        IBillingEmailRepository emails,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await changes.FindAsync(command.PriceChangeId, cancellationToken) is not { MovesAt: { } movesAt } change
            || await contracts.IsContractAsync(cancellationToken)
            || await changes.FindPayerAsync(cancellationToken) is not { Ends: false } payer
            || payer.PlanId != change.PlanId
            || !change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount)
            || await subscriptions.FindSubscriptionAsync(cancellationToken) is not { } subscription
            || !PriceMoves.Takes(subscription, change.PlanId))
        {
            return [];
        }

        var now = time.GetUtcNow();
        var email = await emails.ReadAsync(cancellationToken);
        return
        [
            new NamePriceChangeCommand(
                NoticeIdOf(change.Id, payer.TenantId), now, payer.TenantId, change.PlanId, change.PlanName, change.Currency, payer.Yearly,
                RecordPriceChangeHandler.RecordOf(change.OldPrices), RecordPriceChangeHandler.RecordOf(change.NewPrices), movesAt,
                email?.Address, email?.SetBy),
        ];
    }

    // The change's id and the tenant's id, byte by byte: the same pair gives the same id, another pair another one.
    private static Guid NoticeIdOf(Guid changeId, Guid tenantId) =>
        new([.. changeId.ToByteArray().Zip(tenantId.ToByteArray(), (change, tenant) => (byte)(change ^ tenant))]);
}
