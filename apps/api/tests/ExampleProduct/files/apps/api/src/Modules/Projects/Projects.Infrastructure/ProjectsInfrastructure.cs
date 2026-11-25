using Microsoft.Extensions.DependencyInjection;
using Projects.Application.Projects;
using Projects.Infrastructure.Persistence;
using Projects.Infrastructure.Persistence.Projects;
using Tenancy;
using Wolverine.EntityFrameworkCore;

namespace Projects.Infrastructure;

/// <summary>The module's infrastructure entry point: the host's ProductModules calls it to register the module's DbContext (X8).</summary>
public static class ProjectsInfrastructure
{
    public static IServiceCollection AddProjectsInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<ProjectsDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ProjectsDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<ProjectsDbContext>(ProjectsDbContext.Schema);

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext (W1).
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectQueries, ProjectQueries>();
        return services;
    }
}
