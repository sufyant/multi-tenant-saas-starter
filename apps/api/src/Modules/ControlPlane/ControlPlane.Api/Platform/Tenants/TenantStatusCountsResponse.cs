namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// How many tenants are in each status, without the search or the status filter: the tabs of the Organisations list.
/// </summary>
public sealed record TenantStatusCountsResponse(int Active, int Suspended, int Closed, int Purging, int Purged);
