using SharedKernel;

namespace ControlPlane.Application.Tenants.ReopenTenant;

/// <summary>
/// A system admin gives a closed tenant back to its members, while its reopen period runs (K4). It runs inside that tenant, which the
/// platform route names, with the tenant locked. The invitations the closure cancelled stay cancelled.
/// </summary>
[MessageName("reopen-tenant")]
public sealed record ReopenTenantCommand(string ActorId);
