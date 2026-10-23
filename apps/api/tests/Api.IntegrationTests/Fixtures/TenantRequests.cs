using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

internal static class TenantRequests
{
    public const string Path = "/v1/platform/tenants";

    // POST /v1/platform/tenants is safe to repeat only under the key the client gives it; each call here is a new request unless the
    // test gives the key.
    public static Task<HttpResponseMessage> CreateTenantAsync(this HttpClient client, object body, string? idempotencyKey = null) =>
        client.CreateTenantAsync(JsonContent.Create(body), idempotencyKey);

    public static async Task<HttpResponseMessage> CreateTenantAsync(this HttpClient client, HttpContent body, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = body };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // POST /v1/platform/tenants/{tenantId}/invitations creates an invitation, so it is safe to repeat only under the key the client
    // gives it (I1).
    public static async Task<HttpResponseMessage> InviteOwnerAsync(
        this HttpClient client,
        Guid tenantId,
        string email,
        string? language = null,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Path}/{tenantId}/invitations")
        {
            Content = language is null ? JsonContent.Create(new { email }) : JsonContent.Create(new { email, language }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // POST /v1/platform/tenants/{tenantId}/owners makes a member an Owner. It changes a role and creates nothing, so it takes no
    // idempotency key (I1).
    public static Task<HttpResponseMessage> MakeOwnerAsync(this HttpClient client, Guid tenantId, Guid userId) =>
        client.PostAsJsonAsync($"{Path}/{tenantId}/owners", new { userId }, TestContext.Current.CancellationToken);

    // The system admin's changes of a tenant's name and status. Each sets a state, so it takes no idempotency key (I1).
    public static Task<HttpResponseMessage> RenameTenantAsync(this HttpClient client, Guid tenantId, string name) =>
        client.PatchAsJsonAsync($"{Path}/{tenantId}", new { name }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SuspendTenantAsync(this HttpClient client, Guid tenantId, string reason = "Unpaid invoices") =>
        client.PostAsJsonAsync($"{Path}/{tenantId}/suspend", new { reason }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> CloseTenantAsync(this HttpClient client, Guid tenantId) =>
        client.PostAsync($"{Path}/{tenantId}/close", null, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> ReopenTenantAsync(this HttpClient client, Guid tenantId) =>
        client.PostAsync($"{Path}/{tenantId}/reopen", null, TestContext.Current.CancellationToken);
}
