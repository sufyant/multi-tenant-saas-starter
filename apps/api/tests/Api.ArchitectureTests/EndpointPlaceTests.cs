using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.ArchitectureTests;

// N5 and N7: an endpoint of a module sits in the folder of its audience, the same word as its route group: Admin for /v1/admin,
// Me for /v1/me, Tenant for /v1/tenants/{tenantId} and Webhooks for /v1/webhooks. Read from the endpoints of the composed application: the method that
// handles each one, the type that declares it, and the file of that type in the module's Api project.
public sealed class EndpointPlaceTests : IAsyncLifetime
{
    private static readonly (string RoutePrefix, string Audience)[] Audiences =
    [
        ("/v1/admin/", "Admin"),
        ("/v1/me/", "Me"),
        ("/v1/tenants/{tenantId:guid}/", "Tenant"),
        ("/v1/webhooks/", "Webhooks"),
    ];

    private readonly ComposedApplication _application = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void PlaceEndpoints_EveryModuleEndpoint_SitsInTheFolderOfItsRouteGroup()
    {
        var moduleProjects = Solution.LayerOfEveryModule("Api").ToHashSet();
        var endpoints = _application.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Route: endpoint.RoutePattern.RawText!, Type: DeclaringTypeOf(endpoint)))
            .Where(endpoint => endpoint.Type is not null && moduleProjects.Contains(endpoint.Type.Assembly.GetName().Name!))
            .ToList();

        var misplaced = endpoints
            .Where(endpoint => !SitsInItsAudienceFolder(endpoint.Route, endpoint.Type!))
            .Select(endpoint => $"{endpoint.Route}: {endpoint.Type!.FullName}");

        endpoints.ShouldNotBeEmpty();
        misplaced.ShouldBeEmpty();
    }

    // The outermost type that declares the endpoint's method: a lambda's method sits in a closure type inside it.
    private static Type? DeclaringTypeOf(RouteEndpoint endpoint)
    {
        var type = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType;
        while (type?.DeclaringType is { } outer)
        {
            type = outer;
        }

        return type;
    }

    private static bool SitsInItsAudienceFolder(string route, Type type)
    {
        var audience = Audiences.FirstOrDefault(candidate => route.StartsWith(candidate.RoutePrefix, StringComparison.Ordinal)).Audience;
        if (audience is null)
        {
            return false;
        }

        var project = type.Assembly.GetName().Name!;
        return type.Namespace == $"{project}.{audience}"
            && File.Exists(Path.Combine(Solution.FolderOf(project), audience, $"{type.Name}.cs"));
    }
}
