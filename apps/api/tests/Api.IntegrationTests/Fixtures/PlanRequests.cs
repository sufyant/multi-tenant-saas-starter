using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// The platform's plan endpoints. A value is written as the API reads it: { "enabled": true }, { "maximum": 5 } or { "unlimited": true }.
internal static class PlanRequests
{
    public static object On => new { enabled = true };

    public static object Off => new { enabled = false };

    public static object Unlimited => new { unlimited = true };

    public static object Max(int maximum) => new { maximum };

    // POST /v1/platform/plans creates a plan, so it takes an idempotency key (I1).
    public static async Task<HttpResponseMessage> CreatePlanAsync(this HttpClient client, string name, object entitlements, bool isDefault = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/platform/plans") { Content = JsonContent.Create(new { name, isDefault, entitlements }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> UpdatePlanAsync(this HttpClient client, Guid planId, string name, object entitlements, bool isDefault = false) =>
        client.PatchAsJsonAsync($"/v1/platform/plans/{planId}", new { name, isDefault, entitlements }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetPlanUpdatePreviewAsync(this HttpClient client, Guid planId, object entitlements, string query = "") =>
        client.PostAsJsonAsync($"/v1/platform/plans/{planId}/preview{query}", new { entitlements }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> ChangeTenantPlanAsync(this HttpClient client, Guid tenantId, Guid planId) =>
        client.PutAsJsonAsync($"/v1/platform/tenants/{tenantId}/plan", new { planId }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetTenantPlanChangePreviewAsync(this HttpClient client, Guid tenantId, Guid planId) =>
        client.PostAsJsonAsync($"/v1/platform/tenants/{tenantId}/plan/preview", new { planId }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SetOverrideAsync(this HttpClient client, Guid tenantId, string key, object value, DateTimeOffset? expiresAt = null, string reason = "A trial") =>
        client.PutAsJsonAsync($"/v1/platform/tenants/{tenantId}/entitlement-overrides/{key}", new { value, expiresAt, reason }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RemoveOverrideAsync(this HttpClient client, Guid tenantId, string key) =>
        client.DeleteAsync($"/v1/platform/tenants/{tenantId}/entitlement-overrides/{key}", TestContext.Current.CancellationToken);

    public static Task<JsonElement> TenantPlanAsync(this HttpClient client, Guid tenantId) =>
        client.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenantId}/plan", TestContext.Current.CancellationToken);

    // The entry of one key in a tenant's plan.
    public static JsonElement EntitlementOf(JsonElement tenantPlan, string key) =>
        tenantPlan.GetProperty("entitlements").EnumerateArray().Single(entry => entry.GetProperty("key").GetString() == key);

    public static async Task<(string? Code, string? Entitlement)> ProblemOfAsync(HttpResponseMessage answer)
    {
        var problem = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (problem.GetProperty("code").GetString(), problem.TryGetProperty("entitlement", out var key) ? key.GetString() : null);
    }
}
