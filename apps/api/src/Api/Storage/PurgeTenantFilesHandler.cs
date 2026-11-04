using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace Api.Storage;

// The tenant purge asks the file store to delete every file of the tenant, its prefix tenants/{tenantId}/ (docs/flows/tenant-purge.md).
// No module keeps a tenant's file yet, so the prefix is empty today; a module that keeps one later needs no new step. The handler takes no
// DbContext, so Wolverine opens no transaction around the calls. A failure retries (W11); one that fails for good waits in the dead letter
// queue, and the purge's timeout raises the alarm, as for any step (S10). An empty prefix stays empty, so the step may run twice (S7).
public static class PurgeTenantFilesHandler
{
    /// <summary>The name the host takes part in the purge by, its assembly's (TenantPurgeParticipantTests), which its replies carry.</summary>
    public const string Participant = "Api";

    public static async Task<OutgoingMessages> HandleAsync(PurgeTenantDataCommand purge, IFileStore files, CancellationToken cancellationToken)
    {
        await files.DeletePrefixAsync(FileKeys.Tenant(purge.TenantId), cancellationToken);
        return [new PurgeTenantDataReply(purge.TenantId, Participant)];
    }
}
