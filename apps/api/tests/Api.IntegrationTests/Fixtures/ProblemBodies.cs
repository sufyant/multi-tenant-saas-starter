using System.Text.Json.Nodes;

namespace Api.IntegrationTests.Fixtures;

internal static class ProblemBodies
{
    // Two answers that must tell a caller nothing apart differ only in their trace id, which each request has of its own.
    public static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString();
    }
}
