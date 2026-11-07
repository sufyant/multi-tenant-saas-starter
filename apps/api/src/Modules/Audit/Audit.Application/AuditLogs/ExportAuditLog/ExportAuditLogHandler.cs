using Audit.Contracts.AuditLogs;
using ControlPlane.Contracts.Users;
using Wolverine;

namespace Audit.Application.AuditLogs.ExportAuditLog;

// The audit entry of an export of the Activity log, which the web's server asks for before it writes the file. Audit owns the log, so it
// owns the command; as every command, it publishes one event that Audit handles (K3). The actor is named by user id through ControlPlane's
// contract, and the tenant is the envelope's, which the host declared from the route (T2). It changes no data.
public static class ExportAuditLogHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ExportAuditLogCommand command,
        IMessageContext context,
        IUserDirectory users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(context.TenantId ?? throw new InvalidOperationException("The audit log is exported inside a tenant."));
        var actor = await users.FindContactAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a user.");
        var now = time.GetUtcNow();
        return [new ActivityLogExportedEvent(Guid.CreateVersion7(now), now, tenantId, actor.UserId)];
    }
}
