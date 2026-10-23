using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.UpdateRole"/> did: the role, the name it had, the permissions it added and removed, and whether anything
/// changed (I1).
/// </summary>
internal sealed record RoleUpdate(Role Role, string OldName, IReadOnlySet<string> Added, IReadOnlySet<string> Removed, StateChange Change);
