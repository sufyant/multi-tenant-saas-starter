using SharedKernel;

namespace ControlPlane.Application.Tenants.RenameTenant;

/// <summary>
/// A system admin gives a tenant a new name; the slug never changes (K4). Only the platform renames a tenant: a member cannot. It runs
/// inside that tenant, which the platform route names, with the tenant locked.
/// </summary>
[MessageName("rename-tenant")]
public sealed record RenameTenantCommand(string ActorId, string Name);
