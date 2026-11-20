using SharedKernel;

namespace ControlPlane.Application.Tenants.EndTenantTrial;

/// <summary>
/// The tenant's trial ends: the tenant returns to the plan it had before the trial, if the trial still ends at the moment this message was
/// scheduled for (step 40d). Scheduled at the trial's start, and again at each extension, for the trial's end. It runs in the tenant.
/// </summary>
[MessageName("end-tenant-trial")]
public sealed record EndTenantTrialCommand(Guid TenantId, DateTimeOffset EndsAt);
