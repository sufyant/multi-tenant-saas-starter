using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.price_change_announced</c>, in the tenant's log: the plan, its prices before and after, their currency, whether
/// the tenant pays yearly, and the day from whose next period it pays the new prices (B13, 12.12).
/// </summary>
public sealed record TenantPriceChangeAnnouncedDetails(
    AuditPlan Plan, AuditPlanPrices OldPrices, AuditPlanPrices NewPrices, string Currency, bool Yearly, DateTimeOffset MovesAt) : AuditDetails;
