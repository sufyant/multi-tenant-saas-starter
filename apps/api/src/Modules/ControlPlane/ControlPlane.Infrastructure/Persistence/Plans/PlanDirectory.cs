using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// The plans as billing reads them (step 40). Plans belong to no tenant (R6), so no declaration is needed. The currency is the
// deployment's one setting (BillingOptions), which every price is in. Public because Wolverine's generated code builds it on the caller's
// ControlPlane DbContext (W1, W9).
public sealed class PlanDirectory(ControlPlaneDbContext controlPlane, BillingOptions billing) : IPlanDirectory
{
    public async Task<PlanRecord?> FindPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await Records(controlPlane.Plans.Where(plan => plan.Id == planId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PlanRecord?> FindDefaultPlanAsync(CancellationToken cancellationToken) =>
        await Records(controlPlane.Plans.Where(plan => controlPlane.DefaultPlans.Any(mark => mark.PlanId == plan.Id))).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<PlanRecord> Records(IQueryable<Plan> plans) =>
        plans.Select(plan => new PlanRecord(
            plan.Id,
            plan.Name,
            billing.Currency,
            new PlanPricesRecord(plan.Prices.BaseMonthly, plan.Prices.PerPersonMonthly, plan.Prices.BaseYearly, plan.Prices.PerPersonYearly),
            controlPlane.DefaultPlans.Any(mark => mark.PlanId == plan.Id)));
}
