using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using DomainRetention = ControlPlane.Domain.Plans.AuditRetention;

namespace ControlPlane.Application.Plans;

// K14: a command that changes a tenant's effective values reads them before and after, in the tenant's transaction under its lock, and
// names a shorter Activity log period in its event. The entries that the period before kept go at the event's moment plus the grace.
internal static class RetentionShortenings
{
    public static AuditRetentionShortening? Between(TenantPlanView before, TenantPlanView after, DateTimeOffset now) =>
        DomainRetention.ShorteningOf(PeriodOf(before), PeriodOf(after)) is { } shortening
            ? new AuditRetentionShortening(shortening.FromDays, shortening.ToDays, now.AddDays(DomainRetention.Grace))
            : null;

    private static EntitlementValue PeriodOf(TenantPlanView plan) =>
        plan.Entitlements.Single(entitlement => entitlement.Key == LimitKeys.AuditRetentionDays).Effective.ToValue(LimitKeys.AuditRetentionDays).Value;
}
