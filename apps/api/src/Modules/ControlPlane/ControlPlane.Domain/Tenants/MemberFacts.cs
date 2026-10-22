using ControlPlane.Domain.Roles;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a change to a member needs to know of the tenant's rows (<see cref="Tenant.ChangeRole"/>, <see cref="Tenant.Remove"/>,
/// <see cref="Tenant.RemoveForUserDeletion"/>), read under the tenant's lock (K6j): the target's membership, none when the user is not a
/// member, how many members hold a role with <c>tenant.owners.manage</c>, a deleted user's included (K1), and the tenant's roles, which say
/// what the actor's and the target's roles may do (A1).
/// </summary>
internal sealed record MemberFacts(Membership? Target, int Owners, TenantRoles Roles);
