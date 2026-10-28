using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>The details of <c>tenant.entitlement_override_removed</c>, in the platform's log only: the key and the value and expiry it had.</summary>
public sealed record TenantEntitlementOverrideRemovedDetails(string Key, AuditEntitlementValue OldValue, DateTimeOffset? OldExpiresAt) : AuditDetails;
