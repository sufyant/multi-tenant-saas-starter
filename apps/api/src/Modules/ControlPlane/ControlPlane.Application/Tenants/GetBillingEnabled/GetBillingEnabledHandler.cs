using Billing.Contracts.Tenants;

namespace ControlPlane.Application.Tenants.GetBillingEnabled;

public static class GetBillingEnabledHandler
{
    public static bool Handle(GetBillingEnabledQuery query, IBillingDirectory billing) => billing.Enabled;
}
