using System.Net.Http.Json;
using System.Text.Json;
using Billing.Application.Provider;

namespace Api.IntegrationTests.Fixtures;

// The requests of billing (step 40), on a host whose billing is on, with the fake provider of the factory.
internal static class BillingRequests
{
    // The web app's billing page of a tenant, which Stripe sends the person back to.
    public const string ReturnUrl = "https://app.test/acme/settings/billing";

    public static readonly IReadOnlyDictionary<string, string?> On = new Dictionary<string, string?>
    {
        ["Billing:Payments:Enabled"] = "true",
        ["Billing:Payments:WebUrl"] = ApiFactory.WebUrl,
        ["Billing:Stripe:SecretKey"] = "sk_test_unused",
        ["Billing:Stripe:WebhookSecret"] = "whsec_unused",
    };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> OpenCheckoutSessionAsync(this HttpClient client, Guid tenantId, Guid planId, string interval = "monthly", string returnUrl = ReturnUrl) =>
        client.PostAsJsonAsync($"/v1/tenants/{tenantId}/billing/checkout-session", new { planId, interval, returnUrl }, Cancellation);

    public static Task<HttpResponseMessage> OpenPortalSessionAsync(this HttpClient client, Guid tenantId, string returnUrl = ReturnUrl) =>
        client.PostAsJsonAsync($"/v1/tenants/{tenantId}/billing/portal-session", new { returnUrl }, Cancellation);

    public static Task<HttpResponseMessage> GetBillingAsync(this HttpClient client, Guid tenantId) =>
        client.GetAsync($"/v1/tenants/{tenantId}/billing", Cancellation);

    // A webhook with the fake's body, signed as the fake proves it, or with another signature.
    public static Task<HttpResponseMessage> SendWebhookAsync(this HttpClient client, string eventId, string type, string? subscriptionId, string signature = FakeBillingProvider.Signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/public/billing/webhook")
        {
            Content = new StringContent(FakeBillingProvider.Body(eventId, type, subscriptionId), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);
        return client.SendAsync(request, Cancellation);
    }

    // An active monthly subscription of the tenant at the fake, which the member paid for.
    public static ProviderSubscription ActiveSubscription(Guid tenantId, Guid planId, Guid paidBy, string status = "Active") =>
        new(tenantId, paidBy, $"cus_{tenantId:N}", $"sub_{tenantId:N}", planId, Yearly: false, status, new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero), CancelAtPeriodEnd: false);

    public static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();
}
