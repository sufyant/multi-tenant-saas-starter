using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>What <see cref="Tenant.ChangeRole"/> did: the member, the role they had, and whether the role changed (I1).</summary>
internal sealed record RoleChange(Membership Member, Role OldRole, StateChange Change);
