namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// Why the system admin suspends the tenant, within the API's limit. The API trims the reason before it counts its length. A member
/// never sees it.
/// </summary>
public sealed record SuspendTenantRequest(string Reason);
