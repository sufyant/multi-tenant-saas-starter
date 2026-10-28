using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.entitlement_override_set</c>, in the platform's log only: the key, its old value and expiry (none for a new
/// override), its new value and expiry, and the system admin's reason, the provider's own note.
/// </summary>
public sealed record TenantEntitlementOverrideSetDetails(
    string Key,
    AuditEntitlementValue? OldValue,
    DateTimeOffset? OldExpiresAt,
    AuditEntitlementValue NewValue,
    DateTimeOffset? NewExpiresAt,
    string Reason) : AuditDetails;
