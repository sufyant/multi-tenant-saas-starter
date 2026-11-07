using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.ExportMembers;

// The audit entry of an export of the members (K3), which the web's server asks for before it writes the file. It starts as every tenant
// command does (TenantActor), so a member removed or a tenant suspended while the request waited records nothing. It changes no data. The
// entry keeps no search or filter, which may hold a person's name or address (K3).
public static class ExportMembersHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        ExportMembersCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, []);
        }

        var (tenant, actor, _) = start.Value;
        var now = time.GetUtcNow();
        return (Result.Success(), [new MembersExportedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name)]);
    }
}
