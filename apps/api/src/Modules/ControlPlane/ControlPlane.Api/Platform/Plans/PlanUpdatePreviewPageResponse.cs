namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A page of the tenants a plan edit would put over, in the list contract, and a shorter Activity log period, null when the edit keeps it
/// as long or longer. The period's count is the edit's whole, which no search or page changes.
/// </summary>
public sealed record PlanUpdatePreviewPageResponse(
    IReadOnlyList<TenantExcessResponse> Items,
    int Page,
    int PageSize,
    int Total,
    PlanRetentionShortenedResponse? RetentionShortened);
