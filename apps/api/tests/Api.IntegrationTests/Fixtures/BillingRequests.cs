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

    public static Task<HttpResponseMessage> PreviewPlanChangeAsync(this HttpClient client, Guid tenantId, Guid planId, string interval = "monthly") =>
        client.PostAsJsonAsync($"/v1/tenants/{tenantId}/billing/plan-change/preview", new { planId, interval }, Cancellation);

    public static Task<HttpResponseMessage> ChangePlanAsync(this HttpClient client, Guid tenantId, Guid planId, string interval = "monthly", DateTimeOffset? proratedAt = null) =>
        client.PostAsJsonAsync($"/v1/tenants/{tenantId}/billing/plan-change", new { planId, interval, proratedAt }, Cancellation);

    public static Task<HttpResponseMessage> UndoScheduledChangeAsync(this HttpClient client, Guid tenantId) =>
        client.DeleteAsync($"/v1/tenants/{tenantId}/billing/scheduled-change", Cancellation);

    public static Task<HttpResponseMessage> GetPlanChoicesAsync(this HttpClient client, Guid tenantId) =>
        client.GetAsync($"/v1/tenants/{tenantId}/plans", Cancellation);

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

    // A monthly subscription of the tenant's customer at the fake.
    public static ProviderSubscription Subscription(Guid tenantId, Guid planId, string status = "Active") =>
        new(FakeBillingProvider.CustomerOf(tenantId), $"sub_{tenantId:N}", planId, Yearly: false, status, new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero), CancelAtPeriodEnd: false);

    // The member opens a checkout of the plan, which puts the tenant's customer in our map, then Stripe holds the subscription and sends its
    // event, and the test waits for every message it caused. The plan must cost something. The provider is the host's that handles the sync.
    public static async Task<HttpResponseMessage> PayAsync(this ApiFactory api, string member, Guid tenantId, Guid planId, string status = "Active", ApiFactory? worker = null)
    {
        (await api.CreateClient(member).OpenCheckoutSessionAsync(tenantId, planId)).EnsureSuccessStatusCode();
        (worker ?? api).Billing.Put(Subscription(tenantId, planId, status));
        return await api.WaitingForMessagesAsync(() => api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.created", $"sub_{tenantId:N}"));
    }

    public static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();
}
