namespace ControlPlane.Api.Platform.Tenants;

/// <summary>A page of the tenant list (K5), and how many tenants of the whole list are in each status.</summary>
public sealed record TenantListPageResponse(
    IReadOnlyList<TenantListItemResponse> Items,
    int Page,
    int PageSize,
    int Total,
    TenantStatusCountsResponse StatusCounts);
