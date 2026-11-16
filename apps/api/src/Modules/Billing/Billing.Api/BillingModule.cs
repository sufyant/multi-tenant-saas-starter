using System.Reflection;
using Billing.Api.Platform.Subscriptions;
using Billing.Api.Public.Webhooks;
using Billing.Api.Tenant.CheckoutSessions;
using Billing.Api.Tenant.PortalSessions;
using Billing.Api.Tenant.Subscriptions;
using Billing.Application.Subscriptions.ApplySubscription;
using Billing.Application.Tenants.PurgeTenantData;
using Microsoft.AspNetCore.Routing;

namespace Billing.Api;

/// <summary>The module's API entry point. Its infrastructure is registered by <c>BillingInfrastructure</c>.</summary>
public static class BillingModule
{
    /// <summary>The name Billing takes part in the tenant purge by, for the host to register (docs/flows/tenant-purge.md).</summary>
    public const string TenantPurgeParticipant = PurgeTenantDataHandler.Participant;

    /// <summary>The assembly with the module's handlers, for the host to give Wolverine.</summary>
    public static Assembly HandlerAssembly => typeof(ApplySubscriptionHandler).Assembly;

    /// <summary>
    /// Maps the module's endpoints into the host's route groups: a tenant's billing for the system admins and for the members who manage
    /// it, and Stripe's signed webhook on the public route. Each names its state (A5).
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder platform, RouteGroupBuilder tenant, RouteGroupBuilder publicRoutes)
    {
        PlatformSubscriptionEndpoints.Map(platform);
        SubscriptionEndpoints.Map(tenant);
        CheckoutSessionEndpoints.Map(tenant);
        PortalSessionEndpoints.Map(tenant);
        PlanChangeEndpoints.Map(tenant);
        WebhookEndpoints.Map(publicRoutes);
    }
}
