namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// How many tenants of the list are in each status, with the search and without the status filter: the tabs of the Organisations list.
/// </summary>
public sealed record TenantStatusCountsResponse(int Active, int Suspended, int Closed, int Purging, int Purged);
