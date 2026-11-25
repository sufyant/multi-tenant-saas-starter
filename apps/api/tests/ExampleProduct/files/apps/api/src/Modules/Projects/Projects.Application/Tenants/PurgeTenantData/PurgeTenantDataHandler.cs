using ControlPlane.Contracts.Tenants;
using Projects.Application.Projects;

namespace Projects.Application.Tenants.PurgeTenantData;

// The tenant purge asks the module to delete the tenant's projects, in the tenant's transaction (docs/flows/tenant-purge.md, X8). It
// replies, so the purge goes on; a second copy deletes nothing more (S7).
public static class PurgeTenantDataHandler
{
    /// <summary>The name the module takes part in the purge by, which its replies carry.</summary>
    public const string Participant = "Projects";

    public static async Task<PurgeTenantDataReply> HandleAsync(PurgeTenantDataCommand purge, IProjectRepository projects, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(cancellationToken);
        return new PurgeTenantDataReply(purge.TenantId, Participant);
    }
}
