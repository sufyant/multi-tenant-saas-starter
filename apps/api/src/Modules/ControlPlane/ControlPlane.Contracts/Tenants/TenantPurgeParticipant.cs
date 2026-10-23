namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// A module that takes part in the tenant purge: it handles <see cref="PurgeTenantDataCommand"/> and replies with this name. Each such
/// module's infrastructure entry point registers one as a service, so the purge knows whose replies it waits for without knowing the
/// modules (section 6, "Tenant purge").
/// </summary>
public sealed record TenantPurgeParticipant(string Module);
