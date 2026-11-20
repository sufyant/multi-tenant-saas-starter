using SharedKernel;

namespace ControlPlane.Application.Tenants.WarnTenantTrialEnding;

/// <summary>
/// The tenant's trial ends in <see cref="TrialMessages.WarningDays"/> days: the members who manage billing hear of it, if the trial still
/// ends at the moment this message carries (step 40d; 13.16). Scheduled with the trial's end. It runs in the tenant.
/// </summary>
[MessageName("warn-tenant-trial-end-neared")]
public sealed record WarnTenantTrialEndingCommand(Guid TenantId, DateTimeOffset EndsAt);
