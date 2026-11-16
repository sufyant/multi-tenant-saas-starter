namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPriceChangeAnnouncedDetailsResponse(
    AuditPlanResponse Plan, AuditPlanPricesResponse OldPrices, AuditPlanPricesResponse NewPrices, string Currency, bool Yearly, DateTimeOffset MovesAt)
    : AuditDetailsResponse;
