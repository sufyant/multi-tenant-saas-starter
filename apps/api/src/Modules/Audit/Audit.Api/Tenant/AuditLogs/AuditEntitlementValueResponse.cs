namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// One key's value as an entry keeps it: a feature's state in <c>enabled</c>, or a limit's number in <c>maximum</c>, or an unlimited
/// limit with <c>unlimited</c> true.
/// </summary>
public sealed record AuditEntitlementValueResponse(bool? Enabled, int? Maximum, bool Unlimited);
