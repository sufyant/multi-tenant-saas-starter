using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>An Owner of the tenant, a member whose role holds <c>tenant.owners.manage</c> (A1), with the address and the language to write to.</summary>
public sealed record OwnerAddress(Guid UserId, string Email, Language Language);
