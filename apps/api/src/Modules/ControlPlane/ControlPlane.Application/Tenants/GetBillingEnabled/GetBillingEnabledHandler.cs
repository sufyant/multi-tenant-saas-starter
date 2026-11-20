using Billing.Contracts.Tenants;

namespace ControlPlane.Application.Tenants.GetBillingEnabled;

// Asynchronous, as Wolverine found no handler for the synchronous method that answered a bool.
public static class GetBillingEnabledHandler
{
    public static Task<bool> HandleAsync(GetBillingEnabledQuery query, IBillingDirectory billing) => Task.FromResult(billing.Enabled);
}
