namespace Audit.Api.Tenant.AuditLogs;

public sealed record PlanUpdatedDetailsResponse(
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, AuditEntitlementValueResponse> OldEntitlements,
    IReadOnlyDictionary<string, AuditEntitlementValueResponse> NewEntitlements) : AuditDetailsResponse;
