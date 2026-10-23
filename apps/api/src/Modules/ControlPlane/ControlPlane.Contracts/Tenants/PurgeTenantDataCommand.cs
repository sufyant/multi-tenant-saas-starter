using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>
/// The tenant purge asks each module that takes part in it to delete its data of the tenant (section 6, "Tenant purge"). It travels in
/// the tenant, so each module's handler runs in the tenant's transaction (R4). A handler deletes idempotently and answers with
/// <see cref="PurgeTenantDataReply"/>, which only the purge hears (S13). A module takes part by handling this message and registering its
/// <see cref="TenantPurgeParticipant"/>.
/// </summary>
[MessageName("purge-tenant-data")]
public sealed record PurgeTenantDataCommand(Guid TenantId);
