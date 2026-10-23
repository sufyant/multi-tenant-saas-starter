using System.Reflection;
using Audit.Api.Platform.AuditLogs;
using Audit.Api.Tenant.AuditLogs;
using Audit.Application.Tenants.PurgeTenantData;
using Audit.Application.Tenants.RecordTenantCreated;
using Microsoft.AspNetCore.Routing;

namespace Audit.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>AuditInfrastructure</c>.</summary>
public static class AuditModule
{
    /// <summary>The name Audit takes part in the tenant purge by, for the host to register (docs/flows/tenant-purge.md).</summary>
    public const string TenantPurgeParticipant = PurgeTenantDataHandler.Participant;

    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(RecordTenantCreatedHandler).Assembly;

    /// <summary>
    /// Maps the module's endpoints into the host's route groups: the platform's audit log for system admins, and each tenant's log for
    /// its members. Each names the permission it needs (A5). It reads a page of the log, then has its users named: two
    /// queries, each in the transaction of one module's DbContext (W1).
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder tenant)
    {
        PlatformAuditLogEndpoints.Map(platform);
        AuditLogEndpoints.Map(tenant);
    }
}
