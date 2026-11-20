namespace ControlPlane.Api.Platform.Plans;

/// <summary>The trial's plan, and how many days it lasts from now: 1 to 365.</summary>
public sealed record GiveTenantTrialRequest(Guid PlanId, int Days);
