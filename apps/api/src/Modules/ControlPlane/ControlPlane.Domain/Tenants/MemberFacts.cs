namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a change to a member needs to know of the tenant's rows (<see cref="Tenant.ChangeRole"/>, <see cref="Tenant.Remove"/>,
/// <see cref="Tenant.RemoveForUserDeletion"/>), read under the tenant's lock (K6j): the target's membership, none when the user is not a
/// member, and how many Owners the tenant has, a deleted user's included (K1).
/// </summary>
internal sealed record MemberFacts(Membership? Target, int Owners);
