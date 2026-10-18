using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.ArchitectureTests;

// N5, N7a and N8: an endpoint of a module sits in the folder of its audience, the same word as its route group: Platform for
// /v1/platform, Me for /v1/me and Tenant for /v1/tenants/{tenantId}; below it, in the folder of its resource. Read from the endpoints of the composed application: the method that
// handles each one, the type that declares it, and the file of that type in the module's Api project.
public sealed class EndpointPlaceTests : IAsyncLifetime
{
    private static readonly (string RoutePrefix, string Audience)[] Audiences =
    [
        ("/v1/platform/", "Platform"),
        ("/v1/me/", "Me"),
        ("/v1/tenants/{tenantId:guid}/", "Tenant"),
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

        // N8: below the audience folder, the folder of the resource: Platform/Tenants/TenantEndpoints.cs.
        var project = type.Assembly.GetName().Name!;
        var folders = type.Namespace?.Split('.')[project.Split('.').Length..] ?? [];
        return folders is [var folder, var resource] && folder == audience
            && File.Exists(Path.Combine(Solution.FolderOf(project), audience, resource, $"{type.Name}.cs"));
    }
}
