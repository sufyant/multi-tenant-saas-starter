using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Plans;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// The plans as billing reads them (step 40). Plans belong to no tenant (R6), so no declaration is needed. The currency is the
// deployment's one setting (BillingOptions), which every price is in. Public because Wolverine's generated code builds it on the caller's
// ControlPlane DbContext (W1, W9).
public sealed class PlanDirectory(ControlPlaneDbContext controlPlane, BillingOptions billing) : IPlanDirectory
{
    public async Task<PlanRecord?> FindPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await controlPlane.Plans
            .Where(plan => plan.Id == planId)
            .Select(plan => new PlanRecord(
                plan.Id,
                plan.Name,
                billing.Currency,
                new PlanPricesRecord(plan.Prices.BaseMonthly, plan.Prices.PerPersonMonthly, plan.Prices.BaseYearly, plan.Prices.PerPersonYearly),
                controlPlane.DefaultPlans.Any(mark => mark.PlanId == plan.Id)))
            .SingleOrDefaultAsync(cancellationToken);
}
