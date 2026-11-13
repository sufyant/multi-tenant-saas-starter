namespace ControlPlane.Application.Plans.GetPlanUpdatePreview;

/// <summary>
/// A plan edit that makes the plan's Activity log period shorter (K14, E4): how many tenants on the plan it shortens, those that are not
/// purging or purged and have no override of the key that has not expired, and the plan's period before and after, in days, none for no
/// limit.
/// </summary>
public sealed record PlanRetentionShortenedView(int Count, int? FromDays, int ToDays);
