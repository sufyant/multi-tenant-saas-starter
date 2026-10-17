using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

internal static class InvitationRequests
{
    // POST /v1/tenants/{tenantId}/invitations is safe to repeat only under the key the client gives it; each call here is a new request
    // unless the test gives the key.
    public static async Task<HttpResponseMessage> InviteMemberAsync(
        this HttpClient client,
        Guid tenantId,
        string email,
        string role = "Member",
        string? idempotencyKey = null,
        string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/invitations")
        {
            Content = language is null ? JsonContent.Create(new { email, role }) : JsonContent.Create(new { email, role, language }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
