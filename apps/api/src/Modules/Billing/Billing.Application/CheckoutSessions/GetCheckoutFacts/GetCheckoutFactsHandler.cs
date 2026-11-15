using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Billing.Application.CheckoutSessions.GetCheckoutFacts;

// Reads ControlPlane through its contracts, in the tenant of the message (W2): row level security keeps the seat count to that tenant (R1).
// The tenant door has proved the member, so they are a user.
public static class GetCheckoutFactsHandler
{
    public static async Task<Result<CheckoutFacts>> HandleAsync(
        GetCheckoutFactsQuery query,
        IPlanDirectory plans,
        IMemberDirectory members,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        if (await plans.FindPlanAsync(query.PlanId, cancellationToken) is not { } plan)
        {
            return Errors.PlanNotFound;
        }

        var payer = await users.FindContactAsync(query.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("The tenant door proved the member, so they are a user.");
        var current = await plans.FindPlanIdOfTenantAsync(query.TenantId, cancellationToken);
        var seated = await members.CountSeatedMembersAsync(cancellationToken);
        return new CheckoutFacts(
            payer.UserId, payer.Email, plan.Id, plan.Name, plan.IsDefault, plan.Currency,
            plan.Prices.BaseMonthly, plan.Prices.PerPersonMonthly, plan.Prices.BaseYearly, plan.Prices.PerPersonYearly, current, seated);
    }
}
