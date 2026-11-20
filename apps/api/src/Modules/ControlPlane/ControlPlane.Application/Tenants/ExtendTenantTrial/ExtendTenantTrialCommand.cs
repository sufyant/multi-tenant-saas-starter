using SharedKernel;

namespace ControlPlane.Application.Tenants.ExtendTenantTrial;

/// <summary>
/// A system admin moves the end of the running trial of the tenant the platform route names on, to the moment (step 40d; 4.30). It runs
/// inside that tenant, with the tenant locked. The endpoint lets it run only while billing is on.
/// </summary>
[MessageName("extend-tenant-trial")]
public sealed record ExtendTenantTrialCommand(string ActorId, DateTimeOffset EndsAt);
