namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// Why the system admin suspends the tenant, within the API's limit. The API trims the reason before it counts its length. A member
/// never sees it. It holds no personal data, such as a person's name or email address: the platform's audit log keeps it after the
/// tenant's purge (A8).
/// </summary>
public sealed record SuspendTenantRequest(string Reason);
