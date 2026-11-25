using System.Reflection;

namespace Api.Hosting;

// The product's modules, beside the template's in ModuleComposition and Program (X8). The template ships this file empty and never
// changes it: a product adds a module's handler assembly, its services and its endpoints here, and its projects to ProductModules.props.
// A module that purges a tenant's data registers its TenantPurgeParticipant in AddProductModules, as ModuleComposition does.
internal static class ProductModules
{
    public static Assembly[] HandlerAssemblies => [];

    public static IServiceCollection AddProductModules(this IServiceCollection services) => services;

    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder me, RouteGroupBuilder tenant, RouteGroupBuilder publicRoutes)
    {
    }
}
