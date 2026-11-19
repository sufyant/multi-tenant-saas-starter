using Billing.Contracts.Tenants;

namespace ControlPlane.Application.Tenants.GetPaysByCard;

// Reads Billing through its contract, across tenants (R6).
public static class GetPaysByCardHandler
{
    public static async Task<bool> HandleAsync(GetPaysByCardQuery query, IBillingDirectory billing, CancellationToken cancellationToken) =>
        await billing.PaysByCardAsync(query.TenantId, cancellationToken);
}
