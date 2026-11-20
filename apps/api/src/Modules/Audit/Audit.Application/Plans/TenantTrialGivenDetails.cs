using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>tenant.trial_given</c>: the trial's plan, as it was at that moment, and the trial's end, in both logs.</summary>
public sealed record TenantTrialGivenDetails(AuditPlan Plan, DateTimeOffset EndsAt) : AuditDetails;
