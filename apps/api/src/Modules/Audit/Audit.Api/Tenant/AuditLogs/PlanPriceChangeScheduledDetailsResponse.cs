namespace Audit.Api.Tenant.AuditLogs;

public sealed record PlanPriceChangeScheduledDetailsResponse(AuditPlanPricesResponse OldPrices, AuditPlanPricesResponse NewPrices, string Currency, DateTimeOffset MovesAt)
    : AuditDetailsResponse;
