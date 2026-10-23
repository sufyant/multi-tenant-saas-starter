using ControlPlane.Application.Tenants.TenantPurge;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence;

namespace ControlPlane.Application.Tenants.StartTenantPurge;

// The pivot of the tenant purge (S5), in one transaction under the tenant's lock (K6a): the tenant becomes Purging, so it can no longer be
// reopened, and the saga's record is written with the messages of its first step (S6). A tenant that was reopened, closed again at
// another moment, or whose purge has started does nothing: a reopening cancels no scheduled message, so the message itself checks. The
// participants are the modules the host names (TenantPurgeParticipant); the purge knows no module.
public static class StartTenantPurgeHandler
{
    public static async Task<(Insert<TenantPurgeSaga>?, OutgoingMessages)> HandleAsync(
        StartTenantPurgeCommand start,
        ITenantRepository tenants,
        IEnumerable<TenantPurgeParticipant> participants,
        TenantOptions settings,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant || tenant.BeginPurge(start.ClosedAt) == StateChange.Unchanged)
        {
            return (null, []);
        }

        await tenants.SaveChangesAsync(cancellationToken);
        var (saga, messages) = TenantPurgeSaga.Begin(tenant.Id, participants.Select(participant => participant.Module), settings.PurgeTimeout);
        return (Storage.Insert(saga), messages);
    }
}
