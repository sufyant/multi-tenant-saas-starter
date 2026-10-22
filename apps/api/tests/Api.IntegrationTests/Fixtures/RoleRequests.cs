using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

internal static class RoleRequests
{
    // POST /v1/tenants/{tenantId}/roles is safe to repeat only under the key the client gives it; each call here is a new request unless
    // the test gives the key.
    public static async Task<HttpResponseMessage> CreateRoleAsync(
        this HttpClient client,
        Guid tenantId,
        string name,
        string[] permissions,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/roles") { Content = JsonContent.Create(new { name, permissions }) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> UpdateRoleAsync(this HttpClient client, Guid tenantId, Guid roleId, string name, string[] permissions) =>
        client.PatchAsJsonAsync($"/v1/tenants/{tenantId}/roles/{roleId}", new { name, permissions }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> DeleteRoleAsync(this HttpClient client, Guid tenantId, Guid roleId) =>
        client.DeleteAsync($"/v1/tenants/{tenantId}/roles/{roleId}", TestContext.Current.CancellationToken);
}
