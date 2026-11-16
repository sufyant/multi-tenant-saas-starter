using Billing.Application.PriceChanges.AnnouncePriceChange;
using Billing.Application.PriceChanges.MovePlanPrices;
using Billing.Application.Subscriptions;
using Billing.Contracts.PriceChanges;
using Billing.Domain.PriceChanges;
using ControlPlane.Contracts.Plans;
using Wolverine;

namespace Billing.Application.PriceChanges.RecordPriceChange;

// B13: a plan edit that changed the prices is a price change. Billing keeps it once for each edit (I1), without a tenant, as a plan belongs to
// none (R6), and schedules its notice and its move: the notice 30 days before the move, or at once for a move 30 days away; new customers
// only schedules neither, as Checkout and a plan change already pay the plan's prices (B2). The platform's log records a scheduled move,
// by the system admin (7.11). While billing is off, nobody pays by card, so nothing follows.
public static class RecordPriceChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(PlanUpdatedEvent updated, IPriceChangeStore changes, BillingOptions billing, CancellationToken cancellationToken)
    {
        if (!billing.Enabled || updated is not { PriceChange: { } who, OldPrices: { } oldPrices, NewPrices: { } newPrices, Currency: { } currency })
        {
            return [];
        }

        var change = PriceChange.Of(
            updated.EventId, updated.PlanId, updated.NewName, currency, PricesOf(oldPrices), PricesOf(newPrices), updated.UpdatedBy, updated.OccurredAt,
            Enum.Parse<PriceChangeWho>(who.Who), who.From);
        if (!await changes.AddAsync(change, cancellationToken))
        {
            return [];
        }

        await changes.SaveChangesAsync(cancellationToken);
        if (change.MovesAt is not { } movesAt)
        {
            return [];
        }

        return
        [
            new PriceChangeScheduledEvent(
                Guid.CreateVersion7(change.ChangedAt), change.ChangedAt, change.PlanId, change.PlanName, change.ChangedBy, change.Currency,
                RecordOf(change.OldPrices), RecordOf(change.NewPrices), movesAt),
            .. Scheduled(change, change.ChangedAt),
        ];
    }

    /// <summary>The notice and the move of a change whose move is scheduled: the notice at once when its moment has come.</summary>
    internal static OutgoingMessages Scheduled(PriceChange change, DateTimeOffset now)
    {
        if (change is not { MovesAt: { } movesAt, AnnouncesAt: { } announcesAt })
        {
            return [];
        }

        var announce = new AnnouncePriceChangeCommand(change.Id);
        return [announcesAt > now ? announce.ScheduledAt(announcesAt) : announce, new MovePlanPricesCommand(change.Id).ScheduledAt(movesAt)];
    }

    internal static PriceChangePricesRecord RecordOf(PlanPrices prices) =>
        new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    private static PlanPrices PricesOf(PlanPricesRecord prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
