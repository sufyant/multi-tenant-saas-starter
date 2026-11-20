using ControlPlane.Domain.MeterUsages;
using SharedKernel;

namespace ControlPlane.Application.Usage.GetUsage;

// The page shows three months before this one (11.01): the thread's choice of 40c, as the frames draw them. Only a tenant that pays by
// card, Billing's answer before the query, is offered a cap; a cap left from paying by card shows no more, as it stops nothing (E9).
public static class GetUsageHandler
{
    public const int PastMonths = 3;

    public static async Task<Result<UsageView>> HandleAsync(GetUsageQuery query, IUsageQueries usages, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await usages.FindUsageAsync(MeterUsage.PeriodOf(time.GetUtcNow()), PastMonths, cancellationToken) is not { } usage)
        {
            return Errors.TenantNotFound;
        }

        return usage with { SpendingCap = query.PaysByCard ? usage.SpendingCap : null, SpendingCapOffered = query.PaysByCard };
    }
}
