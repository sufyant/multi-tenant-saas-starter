using ControlPlane.Contracts;

namespace Billing.Application.Subscriptions;

/// <summary>
/// The permissions that Billing's endpoints name, from ControlPlane's one list (D1): the Api project reaches no type of another module's
/// Contracts (section 2), so it reads them here.
/// </summary>
public static class BillingPermissions
{
    public const string TenantBillingManage = Permissions.TenantBillingManage;

    public const string PlatformTenantsRead = Permissions.PlatformTenantsRead;

    /// <summary>How a tenant pays decides who sets its plan, so the switch takes the permission of a tenant's plan (B12).</summary>
    public const string PlatformTenantsChangePlan = Permissions.PlatformTenantsChangePlan;
}
