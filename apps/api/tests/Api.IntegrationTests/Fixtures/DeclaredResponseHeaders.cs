using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// The response headers the OpenAPI document declares on any answer, less the CORS-safelisted ones that a browser script reads without
// the policy (WHATWG Fetch Standard, "CORS-safelisted response-header name"). The CORS tests compare the exposed headers with this set
// rather than with a list of their own (API8).
internal static class DeclaredResponseHeaders
{
    private static readonly string[] Safelisted =
        ["cache-control", "content-language", "content-length", "content-type", "expires", "last-modified", "pragma"];

    public static IEnumerable<string> Of(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .SelectMany(operation => operation.Value.GetProperty("responses").EnumerateObject())
            .SelectMany(answer => answer.Value.TryGetProperty("headers", out var headers) ? headers.EnumerateObject() : [])
            .Select(header => header.Name.ToLowerInvariant())
            .Except(Safelisted)
            .Distinct();
}
