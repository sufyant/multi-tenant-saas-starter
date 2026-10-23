using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// An Owner of the tenant, a member whose role holds <c>tenant.owners.manage</c> (A1), with the address, the language and the time zone
/// to write to. The time zone is an IANA name, or null when it is not set.
/// </summary>
public sealed record OwnerAddress(Guid UserId, string Email, Language Language, string? TimeZone);
