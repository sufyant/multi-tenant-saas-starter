namespace Audit.Api.Tenant.AuditLogs;

/// <summary>A plan's prices as an entry keeps them, each a whole number of the minor unit of the deployment's currency (step 40).</summary>
public sealed record AuditPlanPricesResponse(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
