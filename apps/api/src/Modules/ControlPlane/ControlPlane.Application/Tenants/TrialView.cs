using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The tenant's last trial (step 40d): its plan, when it started, and when it ends, or ended. While it runs, the plan the tenant returns to
/// when it ends; none after its end (4.28, 4.29, 10.08).
/// </summary>
public sealed record TrialView(PlanNames Plan, DateTimeOffset StartedAt, DateTimeOffset EndsAt, PlanNames? ReturnsTo);
