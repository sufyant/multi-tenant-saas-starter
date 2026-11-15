using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Billing.Application.Provider;
using Billing.Infrastructure.Stripe;
using Microsoft.Extensions.Options;
using Stripe;

namespace Billing.UnitTests.Stripe;

// The webhook's signature (A5's exception, step 40): only a body that Stripe signed with the endpoint's secret, recently enough, is read
// (Stripe, "Webhooks", "Verify webhook signatures"). The signature is built here as Stripe builds it: HMAC-SHA256 of "{timestamp}.{body}".
// No call leaves the process: ReadEvent reads no network.
public sealed class StripeBillingProviderTests
{
    private const string Secret = "whsec_test_secret";

    private static readonly IBillingProvider Provider = new StripeBillingProvider(
        new StripeClient("sk_test_unused"),
        Options.Create(new StripeOptions { SecretKey = "sk_test_unused", WebhookSecret = Secret }));

    [Fact]
    public void ReadEvent_ACompletedCheckoutSignedByStripe_NamesItsSubscription()
    {
        var body = EventBody("evt_1", "checkout.session.completed", """{"id": "cs_1", "object": "checkout.session", "subscription": "sub_1"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_1", "checkout.session.completed", "sub_1"));
    }

    [Fact]
    public void ReadEvent_AnUpdatedSubscription_NamesTheSubscription()
    {
        var body = EventBody("evt_2", "customer.subscription.updated", """{"id": "sub_2", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_2", "customer.subscription.updated", "sub_2"));
    }

    // An event about no subscription is kept, and asks for no sync.
    [Fact]
    public void ReadEvent_AnInvoiceEvent_NamesNoSubscription()
    {
        var body = EventBody("evt_3", "invoice.paid", """{"id": "in_3", "object": "invoice"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow));

        read.ShouldBe(new ProviderEvent("evt_3", "invoice.paid", null));
    }

    [Fact]
    public void ReadEvent_SignedWithAnotherSecret_ReadsNothing()
    {
        var body = EventBody("evt_4", "customer.subscription.updated", """{"id": "sub_4", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, "whsec_someone_else", DateTimeOffset.UtcNow));

        read.ShouldBeNull();
    }

    [Fact]
    public void ReadEvent_ABodyChangedAfterItsSignature_ReadsNothing()
    {
        var body = EventBody("evt_5", "customer.subscription.updated", """{"id": "sub_5", "object": "subscription"}""");
        var signature = SignatureOf(body, Secret, DateTimeOffset.UtcNow);

        var read = Provider.ReadEvent(body.Replace("sub_5", "sub_other", StringComparison.Ordinal), signature);

        read.ShouldBeNull();
    }

    // Against a replay: a signature older than the tolerance of 5 minutes is refused.
    [Fact]
    public void ReadEvent_ASignatureOlderThanTheTolerance_ReadsNothing()
    {
        var body = EventBody("evt_6", "customer.subscription.updated", """{"id": "sub_6", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, SignatureOf(body, Secret, DateTimeOffset.UtcNow.AddMinutes(-6)));

        read.ShouldBeNull();
    }

    [Fact]
    public void ReadEvent_WithoutASignature_ReadsNothing()
    {
        var body = EventBody("evt_7", "customer.subscription.updated", """{"id": "sub_7", "object": "subscription"}""");

        var read = Provider.ReadEvent(body, null);

        read.ShouldBeNull();
    }

    private static string EventBody(string id, string type, string dataObject) =>
        $$$"""{"id": "{{{id}}}", "object": "event", "type": "{{{type}}}", "api_version": "{{{StripeConfiguration.ApiVersion}}}", "created": 1, "data": {"object": {{{dataObject}}} }}""";

    private static string SignatureOf(string body, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }
}
