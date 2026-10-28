namespace Audit.Api.Tenant.AuditLogs;

public sealed record PlanCreatedDetailsResponse(bool IsDefault, IReadOnlyDictionary<string, AuditEntitlementValueResponse> Entitlements) : AuditDetailsResponse;
