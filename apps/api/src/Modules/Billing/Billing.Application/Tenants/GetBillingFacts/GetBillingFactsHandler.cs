using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;

namespace Billing.Application.Tenants.GetBillingFacts;

// None for a tenant that does not exist. The system door let the system admin in, so they are a user (A4).
public static class GetBillingFactsHandler
{
    public static async Task<BillingFacts?> HandleAsync(
        GetBillingFactsQuery query,
        ITenantNames tenants,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindNameAsync(query.TenantId, cancellationToken) is not { } name)
        {
            return null;
        }

        var actor = await users.FindContactAsync(query.ExternalUserId, cancellationToken)
            ?? throw new InvalidOperationException("The system door lets in only a user.");
        return new BillingFacts(name, actor.UserId);
    }
}
