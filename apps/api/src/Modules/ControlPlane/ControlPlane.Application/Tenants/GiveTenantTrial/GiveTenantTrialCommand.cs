using SharedKernel;

namespace ControlPlane.Application.Tenants.GiveTenantTrial;

/// <summary>
/// A system admin gives the tenant the platform route names a trial of the plan for the days, from now (step 40d; 4.29). It runs inside
/// that tenant, with the tenant locked. The endpoint lets it run only while billing is on.
/// </summary>
[MessageName("give-tenant-trial")]
public sealed record GiveTenantTrialCommand(string ActorId, Guid PlanId, int Days);
