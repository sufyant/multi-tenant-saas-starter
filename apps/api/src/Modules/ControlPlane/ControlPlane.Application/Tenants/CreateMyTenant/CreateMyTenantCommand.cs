using SharedKernel;

namespace ControlPlane.Application.Tenants.CreateMyTenant;

/// <summary>
/// A user creates a tenant of their own while the product has sign-up on (step 40d, 1.28), and becomes its only Owner at once, with no
/// invitation. It runs inside the tenant it creates, whose id the server chose, as <c>CreateTenant</c> does. The host's idempotency layer
/// protects the HTTP request. The tenant goes on the default plan. While the deployment's billing is on, which the endpoint reads from
/// billing (B1), it starts a trial (step 40d).
/// </summary>
[MessageName("create-my-tenant")]
public sealed record CreateMyTenantCommand(string ActorId, string Name, string Slug, bool BillingEnabled = false);
