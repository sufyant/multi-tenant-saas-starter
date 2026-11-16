using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.PriceChanges.MovePrices;
using Billing.Application.Subscriptions.CancelSubscription;
using Billing.Application.Subscriptions.RefundSubscription;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Application.Subscriptions.SetSeats;
using Billing.Application.Subscriptions.SyncSubscription;
using JasperFx.CodeGeneration;
using JasperFx;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;
using Wolverine;

namespace Billing.Application.Provider;

// A call of the billing provider from a queued message (the read of a subscription after a webhook, and the expiry of a page, the end of a
// subscription at the period's end and its end at once, B7 and B8, the seats of the fee per person, the refund of a second subscription, and a price change's move, B13) that fails is tried again after each configured pause; then the message goes to the dead
// letter queue. Each message has one handler, so a retry may be scheduled (W5). GrowingRetries splits the pauses, as the identity provider's
// retries do. A checkout or a portal page is not retried: the person who asked sees the error and asks again.
internal sealed class BillingProviderRetries(IOptions<BillingProviderOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    private static readonly Type[] Calls =
        [typeof(SyncSubscriptionCommand), typeof(ExpireCheckoutPagesCommand), typeof(SetCancelAtPeriodEndCommand), typeof(CancelSubscriptionCommand),
         typeof(SetSeatsCommand), typeof(RefundSubscriptionCommand), typeof(MovePricesCommand)];

    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(settings.Value.Delays);
        foreach (var chain in chains.Where(chain => Calls.Contains(chain.MessageType)))
        {
            var retries = chain.OnAnyException();
            var next = inline.Count > 0 ? retries.RetryWithCooldown([.. inline]).Then : retries;
            if (scheduled.Count > 0)
            {
                next.ScheduleRetry([.. scheduled]);
            }
        }
    }
}
