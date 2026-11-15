using SharedKernel;

namespace Billing.Application.Provider;

/// <summary>
/// Configuration of the calls that read the billing provider after a webhook (section <c>Billing:Provider</c>), checked on start. The
/// retries grow as the identity provider's do, because the provider may be down for a while. The webhook answered at once, so Stripe does
/// not send the event again: after the last try the sync waits in the dead letter queue.
/// </summary>
public sealed class BillingProviderOptions
{
    public const string Section = "Billing:Provider";

    private static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    // Unset unless configured, so configured pauses replace the defaults: the binder would add them to an array it finds.
    public TimeSpan[]? RetryDelays { get; set; }

    public bool IsValid => GrowingRetries.AreValid(Delays);

    internal IReadOnlyList<TimeSpan> Delays => RetryDelays ?? DefaultRetryDelays;
}
