namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A change that makes the tenant's Activity log period shorter: the period before, in days, null for no limit, and the new one. The
/// entries older than the new period stay 30 days, then go.
/// </summary>
public sealed record RetentionShortenedResponse(int? FromDays, int ToDays);
