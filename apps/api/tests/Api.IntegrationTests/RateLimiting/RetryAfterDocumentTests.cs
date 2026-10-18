using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Api.RateLimiting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Api.IntegrationTests.RateLimiting;

// The maintainer's decision 5 in audit fix 09: the rate limiter answers 429 with Retry-After, the seconds to wait (RFC 9110, section
// 10.2.3), so the document declares the header on the 429 of each operation the limiter covers. The limiter covers an endpoint with
// the general limit (RequireUserRateLimit) or with a policy of its own (RequireRateLimiting).
public sealed class RetryAfterDocumentTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task BuildOpenApiDocument_OperationTheRateLimiterCovers_DeclaresAnIntegerRetryAfterOnItsTooManyRequests()
    {
        var document = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);

        var covered = DocumentedEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<UserRateLimiting.UserRateLimit>() is not null
                || endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
            .Select(endpoint => endpoint.Metadata.OfType<MethodInfo>().First().Name);
        var declaring = Operations(document).Where(DeclaresRetryAfter).Select(operation => operation.GetProperty("operationId").GetString()!);

        covered.ShouldNotBeEmpty();
        declaring.ShouldBe(covered, ignoreOrder: true);
    }

    private IEnumerable<RouteEndpoint> DocumentedEndpoints() =>
        _api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/v1/", StringComparison.Ordinal))
            .Where(endpoint => endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is not { ExcludeFromDescription: true });

    private static IEnumerable<JsonElement> Operations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject().SelectMany(path => path.Value.EnumerateObject().Select(operation => operation.Value));

    private static bool DeclaresRetryAfter(JsonElement operation) =>
        operation.GetProperty("responses").TryGetProperty("429", out var rejected)
        && rejected.TryGetProperty("headers", out var headers)
        && headers.TryGetProperty("Retry-After", out var header)
        && header.GetProperty("schema").GetProperty("type").GetString() == "integer";
}
