using ControlPlane.Application.Plans;
using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>The plan aggregate for the commands. Plans belong to no tenant, so no row level security applies (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class PlanRepository(ControlPlaneDbContext controlPlane) : IPlanRepository
{
    async Task<Guid> IPlanRepository.FindDefaultPlanIdAsync(CancellationToken cancellationToken) =>
        await controlPlane.DefaultPlans.Select(mark => mark.PlanId).SingleAsync(cancellationToken);

    // The row is locked by its own statement, then read with its values, so the lock never sits in a subquery that EF Core composes.
    async Task<Plan?> IPlanRepository.FindPlanForUpdateAsync(Guid planId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT id FROM control_plane.plans WHERE id = {planId} FOR UPDATE", cancellationToken);
        return await controlPlane.Plans.Include(plan => plan.Values).SingleOrDefaultAsync(plan => plan.Id == planId, cancellationToken);
    }

    async Task<PlanNames?> IPlanRepository.FindPlanNameAsync(Guid planId, CancellationToken cancellationToken) =>
        await controlPlane.Plans.Where(plan => plan.Id == planId).Select(plan => new PlanNames(plan.Id, plan.Name)).SingleOrDefaultAsync(cancellationToken);

    async Task<DefaultPlan> IPlanRepository.FindDefaultPlanForUpdateAsync(CancellationToken cancellationToken)
    {
        var found = await controlPlane.DefaultPlans.FromSql($"SELECT * FROM control_plane.default_plans FOR UPDATE").ToListAsync(cancellationToken);
        return found.Single();
    }

    // A name is compared with lower(), as the unique index on lower(name) compares it, so the check and the index never disagree.
    async Task<bool> IPlanRepository.IsNameTakenAsync(string name, Guid? planId, CancellationToken cancellationToken) =>
        await controlPlane.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM control_plane.plans
                WHERE lower(name) = lower({name}) AND id IS DISTINCT FROM {planId}
                """)
            .AnyAsync(cancellationToken);

    void IPlanRepository.Add(Plan plan) => controlPlane.Plans.Add(plan);

    Task IPlanRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
