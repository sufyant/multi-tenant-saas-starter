namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan edit that makes the plan's Activity log period shorter: how many organisations on the plan it shortens, those without an
/// override of the key that has not expired, and the plan's period before, in days, null for no limit, and after.
/// </summary>
public sealed record PlanRetentionShortenedResponse(int Count, int? FromDays, int ToDays);
