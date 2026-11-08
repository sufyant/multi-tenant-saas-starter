using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

// The platform's requests about one user.
internal static class UserRequests
{
    // POST /v1/platform/users/{userId}/suspend suspends the user for the reason, which the request requires.
    public static Task<HttpResponseMessage> SuspendUserAsync(this HttpClient client, Guid userId, string reason = "Shared account") =>
        client.PostAsJsonAsync($"/v1/platform/users/{userId}/suspend", new { reason }, TestContext.Current.CancellationToken);
}
