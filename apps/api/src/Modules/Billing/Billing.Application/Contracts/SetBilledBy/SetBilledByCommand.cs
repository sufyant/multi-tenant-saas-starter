using SharedKernel;

namespace Billing.Application.Contracts.SetBilledBy;

/// <summary>
/// A system admin marks the tenant of the transaction Contract, or takes it back to card (B12, 4.35), with the tenant's name of this moment
/// for the Activity log (K3).
/// </summary>
/// <param name="ActorId">The user id of the system admin.</param>
/// <param name="Contract">True to mark the tenant Contract, false to take it back to card.</param>
[MessageName("set-billed-by")]
public sealed record SetBilledByCommand(Guid ActorId, Guid TenantId, string TenantName, bool Contract);
