namespace ControlPlane.Api.Platform.Plans;

/// <summary>The trial's new end: its end or later, and at most 365 days after its end.</summary>
public sealed record ExtendTenantTrialRequest(DateTimeOffset EndsAt);
