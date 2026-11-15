using Billing.Application.Subscriptions.SyncSubscription;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.Options;
using SharedKernel;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Billing.Application.Provider;

// A read of the billing provider after a webhook that fails is tried again after each configured pause; then the message goes to the dead
// letter queue. The message has one handler, so a retry may be scheduled (W5). GrowingRetries splits the pauses, as the identity provider's
// retries do. A checkout or a portal page is not retried: the person who asked sees the error and asks again.
internal sealed class BillingProviderRetries(IOptions<BillingProviderOptions> settings) : IWolverineExtension, IHandlerPolicy
{
    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(settings.Value.Delays);
        foreach (var chain in chains.Where(chain => chain.MessageType == typeof(SyncSubscriptionCommand)))
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
