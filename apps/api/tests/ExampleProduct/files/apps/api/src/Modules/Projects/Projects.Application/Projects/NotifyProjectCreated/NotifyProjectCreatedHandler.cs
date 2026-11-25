using ControlPlane.Contracts.Members;
using Notifications.Contracts.Notifications;

namespace Projects.Application.Projects.NotifyProjectCreated;

// X7: the module names the people it tells, the tenant's Owners at this moment, in the tenant of the message's envelope, and sends
// Notifications the type's values without the tenant's name. Notifications fans out on the type's channels.
public static class NotifyProjectCreatedHandler
{
    public static async Task<SendNotificationCommand> HandleAsync(NotifyProjectCreatedCommand created, IMemberDirectory members, CancellationToken cancellationToken) =>
        new(
            ProjectsCatalog.ProjectCreated,
            created.EventId,
            created.OccurredAt,
            created.TenantId,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["projectName"] = created.ProjectName, ["actorName"] = created.CreatedBy.ToString() },
            await members.ListOwnerIdsAsync(cancellationToken),
            created.CreatedBy);
}
