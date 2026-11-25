using System.Reflection;
using ControlPlane.Contracts.Tenants;
using Projects.Api;
using Projects.Infrastructure;

namespace Api.Hosting;

// The product's modules, beside the template's in ModuleComposition and Program (X8): the example product's module Projects (X4).
internal static class ProductModules
{
    public static Assembly[] HandlerAssemblies => [ProjectsModule.HandlerAssembly];

    public static IServiceCollection AddProductModules(this IServiceCollection services)
    {
        services.AddProjectsInfrastructure();
        services.AddSingleton(new TenantPurgeParticipant(ProjectsModule.TenantPurgeParticipant));
        return services;
    }

    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder me, RouteGroupBuilder tenant, RouteGroupBuilder publicRoutes) =>
        ProjectsModule.MapEndpoints(tenant);
}
