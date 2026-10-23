using SharedKernel;

namespace ControlPlane.Contracts.Tenants;

/// <summary>A module deleted its data of the tenant, or had none (N3). Only the tenant purge hears it.</summary>
/// <param name="Module">The name the module registered as its <see cref="TenantPurgeParticipant"/>.</param>
[MessageName("purge-tenant-data-reply")]
public sealed record PurgeTenantDataReply(Guid TenantId, string Module);
