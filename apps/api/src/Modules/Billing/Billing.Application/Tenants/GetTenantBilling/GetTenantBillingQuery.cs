using SharedKernel;

namespace Billing.Application.Tenants.GetTenantBilling;

/// <summary>
/// The first step of the platform's read of a tenant's billing, and of its switch (B12): what they read of ControlPlane, through its
/// contracts on ControlPlane's DbContext (W1). The tenant's name of this moment, none for a tenant that does not exist, which the switch
/// keeps in its event for the Activity log (K3), and our user id of the system admin who acts. The rest of the read is billing's own rows.
/// </summary>
/// <param name="ActorId">The identity provider's id of the signed-in system admin (N12).</param>
[MessageName("get-tenant-billing")]
public sealed record GetTenantBillingQuery(string ActorId, Guid TenantId);
