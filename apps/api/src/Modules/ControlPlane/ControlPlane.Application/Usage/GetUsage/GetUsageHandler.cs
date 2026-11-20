using ControlPlane.Domain.MeterUsages;
using SharedKernel;

namespace ControlPlane.Application.Usage.GetUsage;

// The page shows three months before this one (11.01): the thread's choice of 40c, as the frames draw them.
public static class GetUsageHandler
{
    public const int PastMonths = 3;

    public static async Task<Result<UsageView>> HandleAsync(GetUsageQuery query, IUsageQueries usages, TimeProvider time, CancellationToken cancellationToken) =>
        await usages.FindUsageAsync(MeterUsage.PeriodOf(time.GetUtcNow()), PastMonths, cancellationToken) is { } usage ? usage : Errors.TenantNotFound;
}
