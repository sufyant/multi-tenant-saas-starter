namespace ControlPlane.Contracts.Tenants;

/// <summary>A verified membership: the tenant and the permissions the member's role holds there.</summary>
public sealed record TenantMembership(Guid TenantId, IReadOnlySet<string> Permissions);
