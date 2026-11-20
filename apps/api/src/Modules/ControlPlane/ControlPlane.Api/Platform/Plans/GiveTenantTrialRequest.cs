namespace ControlPlane.Api.Platform.Plans;

/// <summary>The trial's plan, and how many days it lasts from now, at least one and at most the most days of a trial.</summary>
public sealed record GiveTenantTrialRequest(Guid PlanId, int Days);
