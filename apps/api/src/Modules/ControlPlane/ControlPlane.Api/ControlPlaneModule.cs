using System.Reflection;
using ControlPlane.Api.Admin;
using ControlPlane.Api.Me;
using ControlPlane.Api.Tenant;
using ControlPlane.Api.Webhooks;
using ControlPlane.Application.Invitations;
using Microsoft.AspNetCore.Routing;

namespace ControlPlane.Api;

/// <summary>
/// The module's API entry point. Its infrastructure is registered by <c>ControlPlaneInfrastructure</c>.
/// </summary>
public static class ControlPlaneModule
{
    /// <summary>The assembly with the module's handlers and validators, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(AcceptInvitationHandler).Assembly;

    /// <summary>
    /// Maps the module's endpoints into the host's route groups, one for each audience. Each endpoint on an admin or tenant route
    /// names the permission it needs, and each webhook endpoint declares that it checks its sender's signature (A5); each sends one
    /// command, and the host maps its result.
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder admin, RouteGroupBuilder me, RouteGroupBuilder tenant, RouteGroupBuilder webhooks)
    {
        Tenants.Map(admin);
        Users.Map(admin);
        MyTenants.Map(me);
        Invitations.Map(me);
        Members.Map(tenant);
        ClerkEvents.Map(webhooks);
    }
}
