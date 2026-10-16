using System.Reflection;
using Audit.Application.Tenants;
using Microsoft.AspNetCore.Routing;

namespace Audit.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>AuditInfrastructure</c>.</summary>
public static class AuditModule
{
    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(RecordTenantCreatedHandler).Assembly;

    /// <summary>
    /// Maps the module's endpoints into the host's route groups: the platform's audit log for system admins, and each tenant's log for
    /// its members. Each names the permission it needs (A5) and sends one query.
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder admin, RouteGroupBuilder tenant)
    {
        Admin.AuditLog.Map(admin);
        Tenant.AuditLog.Map(tenant);
    }
}
