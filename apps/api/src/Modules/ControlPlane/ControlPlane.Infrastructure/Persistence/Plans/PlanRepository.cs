using ControlPlane.Application.Plans;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Plans;

/// <summary>The plan aggregate for the commands. Plans belong to no tenant, so no row level security applies (R6).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class PlanRepository(ControlPlaneDbContext controlPlane) : IPlanRepository
{
    async Task<Guid> IPlanRepository.FindDefaultPlanIdAsync(CancellationToken cancellationToken) =>
        await controlPlane.DefaultPlans.Select(mark => mark.PlanId).SingleAsync(cancellationToken);
}
