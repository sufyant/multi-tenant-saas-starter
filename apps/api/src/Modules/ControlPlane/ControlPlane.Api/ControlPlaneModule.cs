using System.Reflection;
using ControlPlane.Api.Me.Invitations;
using ControlPlane.Api.Me.Tenants;
using ControlPlane.Api.Me.Users;
using ControlPlane.Api.Platform.Invitations;
using ControlPlane.Api.Platform.Members;
using ControlPlane.Api.Platform.Plans;
using ControlPlane.Api.Platform.SystemAdmins;
using ControlPlane.Api.Platform.Tenants;
using ControlPlane.Api.Platform.Users;
using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Api.Tenant.Tenants;
using ControlPlane.Application.Invitations.AcceptInvitation;
using Microsoft.AspNetCore.Routing;
using ControlPlane.Api.Tenant.Roles;

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
        TenantEndpoints.Map(platform);
        PlatformInvitationEndpoints.Map(platform);
        PlatformMemberEndpoints.Map(platform);
        PlanEndpoints.Map(platform);
        UserEndpoints.Map(platform);
        SystemAdminEndpoints.Map(platform);
        MyProfileEndpoints.Map(me);
        MyTenantEndpoints.Map(me);
        MyInvitationEndpoints.Map(me);
        TenantProfileEndpoints.Map(tenant);
        MemberEndpoints.Map(tenant);
        InvitationEndpoints.Map(tenant);
        RoleEndpoints.Map(tenant);
    }
}
