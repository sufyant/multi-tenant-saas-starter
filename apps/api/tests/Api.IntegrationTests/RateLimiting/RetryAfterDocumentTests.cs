using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tenancy;

namespace Api.IntegrationTests.RateLimiting;

// The maintainer's decision 5 in audit fix 09: the rate limiter answers 429 with Retry-After, the seconds to wait (RFC 9110, section
// 10.2.3), so the document declares the header on the 429 of each operation the limiter covers, and of no other. The limiter counts
// before anything asks who the caller is, so an anonymous caller is limited by its address: with one request in the window, the next
// request to an operation the limiter covers answers 429, and one to any other operation answers 401.
public sealed partial class RetryAfterDocumentTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "1", ["RateLimiting:Window"] = "01:00:00" });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task BuildOpenApiDocument_OperationTheRateLimiterCovers_DeclaresAnIntegerRetryAfterOnItsTooManyRequests()
    {
        var document = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);
        var anonymous = _api.CreateClient();
        await anonymous.GetAsync("/v1/me", Cancellation);

        var answers = await Task.WhenAll(Operations(document).Select(async operation =>
            (operation.Id, Status: (await anonymous.SendAsync(new HttpRequestMessage(operation.Method, operation.Path), Cancellation)).StatusCode)));
        var covered = answers.Where(answer => answer.Status == HttpStatusCode.TooManyRequests).Select(answer => answer.Id);
        var declaring = Operations(document).Where(operation => DeclaresRetryAfter(operation.Value)).Select(operation => operation.Id);

        covered.ShouldNotBeEmpty();
        declaring.ShouldBe(covered, ignoreOrder: true);
    }

    // Each operation of the document, at a path whose parameters are new ids.
    private static IEnumerable<(string Id, HttpMethod Method, string Path, JsonElement Value)> Operations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (
                operation.Value.GetProperty("operationId").GetString()!,
                new HttpMethod(operation.Name.ToUpperInvariant()),
                PathParameter().Replace(path.Name, _ => Guid.NewGuid().ToString()),
                operation.Value)));

    private static bool DeclaresRetryAfter(JsonElement operation) =>
        operation.GetProperty("responses").TryGetProperty("429", out var rejected)
        && rejected.TryGetProperty("headers", out var headers)
        && headers.TryGetProperty("Retry-After", out var header)
        && header.GetProperty("schema").GetProperty("type").GetString() == "integer";

    [GeneratedRegex(@"\{\w+\}")]
    private static partial Regex PathParameter();
}
