using SharedKernel;

namespace Billing.Application.Tenants.GetBillingFacts;

/// <summary>
/// What the platform's billing endpoints read of ControlPlane, through its contracts on ControlPlane's DbContext (W1): the tenant's name
/// of this moment, none for a tenant that does not exist, which the Contract switch keeps in its event for the Activity log (K3), and our
/// user id of the signed-in system admin.
/// </summary>
/// <param name="ExternalUserId">The identity provider's id of the signed-in system admin.</param>
[MessageName("get-billing-facts")]
public sealed record GetBillingFactsQuery(Guid TenantId, string ExternalUserId);
