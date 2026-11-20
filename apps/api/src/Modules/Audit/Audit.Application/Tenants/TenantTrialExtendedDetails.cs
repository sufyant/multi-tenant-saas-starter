using Audit.Application.AuditLogs;

namespace Audit.Application.Tenants;

/// <summary>The details of <c>tenant.trial_extended</c>: the trial's end before and after, in both logs.</summary>
public sealed record TenantTrialExtendedDetails(DateTimeOffset OldEndsAt, DateTimeOffset NewEndsAt) : AuditDetails;
