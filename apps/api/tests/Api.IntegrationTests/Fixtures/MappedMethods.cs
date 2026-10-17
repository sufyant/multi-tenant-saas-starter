using Api.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests.Fixtures;

// The HTTP methods of the endpoints a host has mapped under /v1, read from the host's routes. The CORS tests compare the preflight's
// allowed methods with this set rather than with a list of their own (API8).
internal static class MappedMethods
{
    public static IEnumerable<string> Of(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith($"{RouteGroups.V1}/", StringComparison.Ordinal) == true)
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            .Distinct();
}
