using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Projects.Api.Tenant.Projects;
using Projects.Application.Projects.CreateProject;
using Projects.Application.Tenants.PurgeTenantData;

namespace Projects.Api;

/// <summary>The module's API entry point, which the host's ProductModules names (X8). Its infrastructure is registered by <c>ProjectsInfrastructure</c>.</summary>
public static class ProjectsModule
{
    /// <summary>The name the module takes part in the tenant purge by, for the host to register (docs/flows/tenant-purge.md).</summary>
    public const string TenantPurgeParticipant = PurgeTenantDataHandler.Participant;

    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(CreateProjectHandler).Assembly;

    /// <summary>Maps the module's endpoints into the host's tenant route group (A5).</summary>
    public static void MapEndpoints(RouteGroupBuilder tenant) => ProjectEndpoints.Map(tenant);
}
