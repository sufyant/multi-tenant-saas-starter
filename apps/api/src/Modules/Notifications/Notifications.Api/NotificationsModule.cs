using System.Reflection;
using Notifications.Application.Invitations.SendInvitationEmail;

namespace Notifications.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>NotificationsInfrastructure</c>.</summary>
public static class NotificationsModule
{
    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(SendInvitationEmailHandler).Assembly;
}
