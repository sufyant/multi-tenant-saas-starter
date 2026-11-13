namespace ControlPlane.Application.Plans;

/// <summary>
/// A change that makes the tenant's Activity log period shorter (K14): the period before, in days, none for no limit, and the new one. The
/// entries outside the new period stay 30 days after the change, then go.
/// </summary>
public sealed record RetentionShorteningView(int? FromDays, int ToDays);
