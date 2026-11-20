using System.Net.Http.Json;

namespace Api.IntegrationTests.Fixtures;

// The requests of Usage (step 40c).
internal static class UsageRequests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> SetSpendingCapAsync(this HttpClient client, Guid tenantId, long? amount) =>
        client.PutAsJsonAsync($"/v1/tenants/{tenantId}/usage/spending-cap", new { amount }, Cancellation);
}
