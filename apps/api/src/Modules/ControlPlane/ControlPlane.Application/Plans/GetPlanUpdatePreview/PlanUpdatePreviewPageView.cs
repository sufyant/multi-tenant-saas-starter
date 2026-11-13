using SharedKernel;

namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

/// <summary>
/// The preview of a plan edit: a page of the tenants it would put over, and a shorter Activity log period, none when the edit makes it as
/// long or longer. The count of the period is the edit's whole, which no search or page changes.
/// </summary>
public sealed record PlanUpdatePreviewPageView(ListPage<TenantExcessView> Page, PlanRetentionShortenedView? RetentionShortened);
