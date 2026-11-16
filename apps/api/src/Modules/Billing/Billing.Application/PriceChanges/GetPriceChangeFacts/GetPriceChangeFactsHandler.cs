using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;

namespace Billing.Application.PriceChanges.GetPriceChangeFacts;

// None for a plan that does not exist. The system door let the system admin in, so they are a user (A4).
public static class GetPriceChangeFactsHandler
{
    public static async Task<PriceChangeFacts?> HandleAsync(GetPriceChangeFactsQuery query, IPlanDirectory plans, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (await plans.FindPlanAsync(query.PlanId, cancellationToken) is not { } plan)
        {
            return null;
        }

        var actor = await users.FindContactAsync(query.ActorId, cancellationToken) ?? throw new InvalidOperationException("The system door lets in only a user.");
        return new PriceChangeFacts(plan.Id, actor.UserId);
    }
}
