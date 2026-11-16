using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>plan.price_change_scheduled</c>, in the platform's log: the plan's prices before and after, their currency, and the day
/// its payers move to the new prices (B13, 7.11).
/// </summary>
public sealed record PlanPriceChangeScheduledDetails(AuditPlanPrices OldPrices, AuditPlanPrices NewPrices, string Currency, DateTimeOffset MovesAt) : AuditDetails;
