using System.Text.Json.Nodes;
using ControlPlane.Api.Platform.Plans;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Plans.GetPlanChoices;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.Plans;

/// <summary>The plans that a member who manages billing can move the tenant to (step 40, 10.02).</summary>
public static class PlanChoiceEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/plans", GetPlanChoices).RequireAuthorization(Permissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    private static async Task<Result<PlanChoicesResponse>> GetPlanChoices(IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        new PlanChoicesResponse([.. (await bus.InvokeAsync<IReadOnlyList<PlanView>>(new GetPlanChoicesQuery(), cancellationToken)).Select(plan => new PlanChoiceResponse(
            plan.Id,
            plan.Name,
            plan.IsDefault,
            plan.Entitlements.OrderBy(value => value.Key, StringComparer.Ordinal).ToDictionary(value => value.Key, value => (JsonNode?)ValueOf(value.Value), StringComparer.Ordinal),
            billing.Currency,
            PricesOf(plan.Prices ?? PlanPricesView.Zero),
            [.. (plan.Meters ?? []).Select(meter => new MeterSettingResponse(meter.Key, meter.Quota, ResponseStatus.OfMeterOverage(meter.Overage), meter.UnitPrice, meter.UnitSize))]))]);

    private static PlanPricesResponse PricesOf(PlanPricesView prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    // A feature is true or false, a limit is its number, and an unlimited limit is null, as GetMyTenant answers them.
    private static JsonValue? ValueOf(EntitlementValueView value) =>
        value.Enabled is { } enabled ? JsonValue.Create(enabled) : value.Maximum is { } maximum ? JsonValue.Create(maximum) : null;
}
