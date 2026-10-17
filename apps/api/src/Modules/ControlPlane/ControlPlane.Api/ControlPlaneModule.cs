using System.Reflection;
using ControlPlane.Api.Platform;
using ControlPlane.Api.Me;
using ControlPlane.Api.Tenant;
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
    /// Maps the module's endpoints into the host's route groups, one for each audience. Each endpoint on a platform or tenant route
    /// names the permission it needs (A5); each sends one command, and the host maps its result. Deleting a user sends one command for
    /// each step, because each step runs in a transaction of its own.
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder me, RouteGroupBuilder tenant)
    {
        Tenants.Map(platform);
        Users.Map(platform);
        MyProfile.Map(me);
        MyTenants.Map(me);
        Me.Invitations.Map(me);
        Profile.Map(tenant);
        Members.Map(tenant);
        Tenant.Invitations.Map(tenant);
    }
}
