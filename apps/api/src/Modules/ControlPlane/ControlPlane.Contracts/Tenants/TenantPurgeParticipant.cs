namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A module that takes part in the tenant purge: it handles <see cref="PurgeTenantDataCommand"/> and replies with this name. The host's
/// composition registers one as a service for each such module, so the purge knows whose replies it waits for without knowing the
/// modules (docs/flows/tenant-purge.md).
/// </summary>
public sealed record TenantPurgeParticipant(string Module);
