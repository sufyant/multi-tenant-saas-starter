using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

internal static class InvitationRequests
{
    // POST /v1/tenants/{tenantId}/invitations is safe to repeat only under the key the client gives it; each call here is a new request
    // unless the test gives the key. The role is a role's name, which the client reads as an id from the tenant's roles (Roles.IdOfAsync),
    // or an id.
    public static async Task<HttpResponseMessage> InviteMemberAsync(
        this HttpClient client,
        Guid tenantId,
        string email,
        string role = "member",
        string? idempotencyKey = null,
        string? language = null)
    {
        var roleId = Guid.TryParse(role, out var id) ? id : await client.IdOfAsync(tenantId, role);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/tenants/{tenantId}/invitations")
        {
            Content = language is null ? JsonContent.Create(new { email, roleId }) : JsonContent.Create(new { email, roleId, language }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
