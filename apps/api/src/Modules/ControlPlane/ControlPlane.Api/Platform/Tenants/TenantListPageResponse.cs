namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A page of the tenant list (K5), and how many tenants of the whole list are in each status and, while billing is on, in each billing
/// situation (4.38); null while billing is off (B1).
/// </summary>
public sealed record TenantListPageResponse(
    IReadOnlyList<TenantListItemResponse> Items,
    int Page,
    int PageSize,
    int Total,
    TenantStatusCountsResponse StatusCounts,
    TenantBillingCountsResponse? BillingCounts);
