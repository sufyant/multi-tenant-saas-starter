namespace Audit.Api.Tenant.AuditLogs;

public sealed record PlanUpdatedDetailsResponse(
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, AuditEntitlementValueResponse> OldEntitlements,
    IReadOnlyDictionary<string, AuditEntitlementValueResponse> NewEntitlements,
    AuditPlanResponse? PreviousDefaultPlan,
    AuditPlanPricesResponse? OldPrices = null,
    AuditPlanPricesResponse? NewPrices = null,
    IReadOnlyList<AuditMeterSettingResponse>? OldMeters = null,
    IReadOnlyList<AuditMeterSettingResponse>? NewMeters = null) : AuditDetailsResponse;
