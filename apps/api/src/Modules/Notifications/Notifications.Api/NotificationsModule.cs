using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Notifications.Api.Me.EmailPreferences;
using Notifications.Api.Me.Notifications;
using Notifications.Api.Platform.Notifications;
using Notifications.Api.Tenant.Notifications;
using Notifications.Application.Invitations.SendInvitationEmail;
using Notifications.Application.Tenants.PurgeTenantData;

namespace Notifications.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>NotificationsInfrastructure</c>.</summary>
public static class NotificationsModule
{
    /// <summary>The name Notifications takes part in the tenant purge by, for the host to register (docs/flows/tenant-purge.md).</summary>
    public const string TenantPurgeParticipant = PurgeTenantDataHandler.Participant;

    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(SendInvitationEmailHandler).Assembly;

    /// <summary>Maps the module's endpoints into the host's route groups: the bells of the tenant web, the personal one and the platform's (U7).</summary>
    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder me, RouteGroupBuilder tenant)
    {
        NotificationEndpoints.Map(tenant);
        MyNotificationEndpoints.Map(me);
        EmailPreferenceEndpoints.Map(me);
        PlatformNotificationEndpoints.Map(platform);
    }
}
