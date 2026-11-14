using Audit.Application.AuditLogs;

namespace Audit.Application.FeatureFlags;

/// <summary>The details of <c>feature_flag.tenant_listed</c>: the organisation, with its name of that moment, which is not a person's (K3).</summary>
public sealed record FeatureFlagTenantListedDetails(Guid TenantId, string TenantName) : AuditDetails;
