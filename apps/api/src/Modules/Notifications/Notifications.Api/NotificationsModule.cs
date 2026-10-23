using System.Reflection;
using Notifications.Application.Invitations.SendInvitationEmail;
using Notifications.Application.Tenants.PurgeTenantData;

namespace Notifications.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>NotificationsInfrastructure</c>.</summary>
public static class NotificationsModule
{
    /// <summary>The name Notifications takes part in the tenant purge by, for the host to register (section 6, "Tenant purge").</summary>
    public const string TenantPurgeParticipant = PurgeTenantDataHandler.Participant;

    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(SendInvitationEmailHandler).Assembly;
}
