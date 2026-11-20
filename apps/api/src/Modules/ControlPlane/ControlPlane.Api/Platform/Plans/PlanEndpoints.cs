using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Api.Statuses;
using ControlPlane.Application;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Plans.CreatePlan;
using ControlPlane.Application.Plans.GetPlan;
using ControlPlane.Application.Tenants.GetPaysByCard;
using ControlPlane.Application.Plans.ListPlans;
using ControlPlane.Application.Plans.GetPlanUpdatePreview;
using ControlPlane.Application.Plans.UpdatePlan;
using ControlPlane.Application.Tenants.ChangeTenantPlan;
using ControlPlane.Application.Tenants.ExtendTenantTrial;
using ControlPlane.Application.Tenants.GetBillingEnabled;
using ControlPlane.Application.Tenants.GiveTenantTrial;
using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Tenants.GetTenantPlanChangePreview;
using ControlPlane.Application.Tenants.RemoveEntitlementOverride;
using ControlPlane.Application.Tenants.SetEntitlementOverride;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// The plans a system admin manages, and each tenant's plan and overrides (docs/flows/plans-and-entitlements.md). Plans belong to no
/// tenant; a tenant's plan and overrides are read and changed inside the tenant the route names (W3).
/// </summary>
public static class PlanEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/plans", ListPlans).RequireAuthorization(Permissions.PlatformPlansRead);
        platform.MapGet("/plans/{planId:guid}", GetPlan).WithName(nameof(GetPlan)).RequireAuthorization(Permissions.PlatformPlansRead);
        platform.MapPost("/plans", CreatePlan)
            .WithMetadata(new IdempotencyKeyRequired(), CreatedLocation.Of<PlanResponse>(nameof(GetPlan), plan => new { planId = plan.Id }))
            .RequireAuthorization(Permissions.PlatformPlansCreate)
            .Produces<PlanResponse>(StatusCodes.Status201Created);
        platform.MapPatch("/plans/{planId:guid}", UpdatePlan).RequireAuthorization(Permissions.PlatformPlansUpdate);
        platform.MapPost("/plans/{planId:guid}/preview", GetPlanUpdatePreview).RequireAuthorization(Permissions.PlatformPlansUpdate);
        platform.MapGet("/tenants/{tenantId:guid}/plan", GetTenantPlan).RequireAuthorization(Permissions.PlatformTenantsRead);
        platform.MapPut("/tenants/{tenantId:guid}/plan", ChangeTenantPlan).RequireAuthorization(Permissions.PlatformTenantsChangePlan);
        platform.MapPost("/tenants/{tenantId:guid}/plan/preview", GetTenantPlanChangePreview).RequireAuthorization(Permissions.PlatformTenantsChangePlan);
        platform.MapPost("/tenants/{tenantId:guid}/trial", GiveTenantTrial)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription("Answers 201 with the tenant's plan and its trial, without Location: GetTenantPlan reads the trial.")
            .RequireAuthorization(Permissions.PlatformTenantsChangePlan)
            .Produces<TenantPlanResponse>(StatusCodes.Status201Created);
        platform.MapPatch("/tenants/{tenantId:guid}/trial", ExtendTenantTrial).RequireAuthorization(Permissions.PlatformTenantsChangePlan);
        platform.MapPut("/tenants/{tenantId:guid}/entitlement-overrides/{key}", SetEntitlementOverride)
            .RequireAuthorization(Permissions.PlatformTenantsOverrideEntitlements);
        platform.MapDelete("/tenants/{tenantId:guid}/entitlement-overrides/{key}", RemoveEntitlementOverride)
            .RequireAuthorization(Permissions.PlatformTenantsOverrideEntitlements);
    }

    private static async Task<Result<ListPage<PlanListItemResponse>>> ListPlans(
        int? page,
        int? pageSize,
        ListSort<ListPlansSort>? sort,
        string? q,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var plans = await bus.InvokeAsync<Result<ListPage<PlanListItemView>>>(new ListPlansQuery(list.Value), cancellationToken);
        return plans.Map(found => found.Map(plan => new PlanListItemResponse(plan.Id, plan.Name, plan.IsDefault, plan.TenantCount, plan.CreatedAt)));
    }

    private static async Task<Result<PlanResponse>> GetPlan(Guid planId, IMessageBus bus, BillingOptions billing, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<PlanView>>(new GetPlanQuery(planId), cancellationToken)).Map(plan => ToResponse(plan, billing));

    // The client gives each new plan an idempotency key (I1); the endpoint declares 201, with the plan's address, where GetPlan reads it.
    private static async Task<Result<PlanResponse>> CreatePlan(
        CreatePlanRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        BillingOptions billing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<PlanView>(
            new CreatePlanCommand(user.Id(), request.Name, request.IsDefault, ToViews(request.Entitlements), ToView(request.Prices), ToViews(request.Meters)),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken);
        return created.Map(plan => ToResponse(plan, billing));
    }

    // An edit sets a state, so it takes no idempotency key (I1).
    private static async Task<Result<PlanResponse>> UpdatePlan(
        Guid planId,
        UpdatePlanRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        BillingOptions billing,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<PlanView>>(
            new UpdatePlanCommand(user.Id(), planId, request.Name, request.IsDefault, ToViews(request.Entitlements), ToView(request.Prices), ToViews(request.Meters),
                request.PriceChange is { } change ? new PriceChangeView(change.Who.ToString(), change.From) : null),
            cancellationToken)).Map(plan => ToResponse(plan, billing));

    // A preview only reads, so it takes no idempotency key (I1). It reads across tenants, so it runs without one.
    private static async Task<Result<PlanUpdatePreviewPageResponse>> GetPlanUpdatePreview(
        Guid planId,
        GetPlanUpdatePreviewRequest request,
        int? page,
        int? pageSize,
        ListSort<GetPlanUpdatePreviewSort>? sort,
        string? q,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var preview = await bus.InvokeAsync<Result<PlanUpdatePreviewPageView>>(
            new GetPlanUpdatePreviewQuery(planId, ToViews(request.Entitlements), list.Value), cancellationToken);
        return preview.Map(found => new PlanUpdatePreviewPageResponse(
            [.. found.Page.Items.Select(tenant => new TenantExcessResponse(
                tenant.TenantId, tenant.Name, tenant.Slug, tenant.Furthest, [.. tenant.Excesses.Select(ToResponse)], tenant.OverriddenKeys))],
            found.Page.Page,
            found.Page.PageSize,
            found.Page.Total,
            found.RetentionShortened is { } shortened ? new PlanRetentionShortenedResponse(shortened.Count, shortened.FromDays, shortened.ToDays) : null));
    }

    private static async Task<Result<TenantPlanResponse>> GetTenantPlan(Guid tenantId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantPlanView>>(tenantId.ToString(), new GetTenantPlanQuery(), cancellationToken)).Map(ToResponse);

    // A change of the tenant's plan sets a state, so it takes no idempotency key (I1). B6: a tenant that pays by card is on the plan its
    // subscription pays for, so a system admin sets the plan of a Contract tenant only (B12). Billing is read before the command, on its
    // own DbContext (W1); a first payment that comes between them moves the plan again (B6).
    private static async Task<Result<TenantPlanResponse>> ChangeTenantPlan(
        Guid tenantId,
        ChangeTenantPlanRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (await bus.InvokeAsync<bool>(new GetPaysByCardQuery(tenantId), cancellationToken))
        {
            return Errors.TenantPaysByCard;
        }

        return (await bus.InvokeForTenantAsync<Result<TenantPlanView>>(tenantId.ToString(), new ChangeTenantPlanCommand(user.Id(), request.PlanId), cancellationToken))
            .Map(ToResponse);
    }

    // Step 40d: a trial exists only while billing is on, so both answer 404 trial.billing_off while it is off. Billing is read before the
    // command, as one handler has one DbContext (W1). Giving a trial starts a process, the trial and its scheduled end, so it takes an
    // idempotency key and answers 201 (I1, I5). An extension names the trial's new end, not the days it adds: it sets a state and takes none.
    private static async Task<Result<TenantPlanResponse>> GiveTenantTrial(
        Guid tenantId,
        GiveTenantTrialRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        if (!await bus.InvokeAsync<bool>(new GetBillingEnabledQuery(), cancellationToken))
        {
            return Errors.TrialBillingOff;
        }

        return (await idempotency.ExecuteAsync<TenantPlanView>(
            new GiveTenantTrialCommand(user.Id(), request.PlanId, request.Days), tenantId.ToString(), cancellationToken)).Map(ToResponse);
    }

    private static async Task<Result<TenantPlanResponse>> ExtendTenantTrial(
        Guid tenantId,
        ExtendTenantTrialRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!await bus.InvokeAsync<bool>(new GetBillingEnabledQuery(), cancellationToken))
        {
            return Errors.TrialBillingOff;
        }

        return (await bus.InvokeForTenantAsync<Result<TenantPlanView>>(
            tenantId.ToString(), new ExtendTenantTrialCommand(user.Id(), request.EndsAt), cancellationToken)).Map(ToResponse);
    }

    private static async Task<Result<TenantPlanChangePreviewResponse>> GetTenantPlanChangePreview(
        Guid tenantId,
        ChangeTenantPlanRequest request,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantPlanChangePreviewView>>(tenantId.ToString(), new GetTenantPlanChangePreviewQuery(request.PlanId), cancellationToken))
            .Map(preview => new TenantPlanChangePreviewResponse(
                [.. preview.Excesses.Select(ToResponse)],
                preview.OverriddenKeys,
                preview.RetentionShortened is { } shortened ? new RetentionShortenedResponse(shortened.FromDays, shortened.ToDays) : null));

    // Setting an override sets a state, so it takes no idempotency key (I1).
    private static async Task<Result<TenantPlanResponse>> SetEntitlementOverride(
        Guid tenantId,
        string key,
        SetEntitlementOverrideRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantPlanView>>(
            tenantId.ToString(),
            new SetEntitlementOverrideCommand(user.Id(), key, ToView(request.Value), request.ExpiresAt, request.Reason),
            cancellationToken)).Map(ToResponse);

    private static async Task<Result> RemoveEntitlementOverride(
        Guid tenantId,
        string key,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        await bus.InvokeForTenantAsync<Result>(tenantId.ToString(), new RemoveEntitlementOverrideCommand(user.Id(), key), cancellationToken);

    private static Dictionary<string, EntitlementValueView> ToViews(IReadOnlyDictionary<string, EntitlementValueRequest>? values) =>
        (values ?? new Dictionary<string, EntitlementValueRequest>()).ToDictionary(value => value.Key, value => ToView(value.Value), StringComparer.Ordinal);

    private static EntitlementValueView ToView(EntitlementValueRequest? value) => new(value?.Enabled, value?.Maximum, value?.Unlimited ?? false);

    private static EntitlementValueResponse ToResponse(EntitlementValueView value) => new(value.Enabled, value.Maximum, value.Unlimited);

    private static ExcessResponse ToResponse(ExcessView excess) => new(excess.Key, excess.Count, ToResponse(excess.NewValue), excess.Amount);

    private static PlanResponse ToResponse(PlanView plan, BillingOptions billing) =>
        new(
            plan.Id,
            plan.Name,
            plan.IsDefault,
            plan.TenantCount,
            plan.Entitlements.OrderBy(value => value.Key, StringComparer.Ordinal).ToDictionary(value => value.Key, value => ToResponse(value.Value), StringComparer.Ordinal),
            billing.Currency,
            ToResponse(plan.Prices ?? PlanPricesView.Zero),
            [.. (plan.Meters ?? []).Select(meter => new MeterSettingResponse(meter.Key, meter.Quota, ResponseStatus.OfMeterOverage(meter.Overage), meter.UnitPrice, meter.UnitSize))],
            plan.CreatedAt);

    private static PlanPricesResponse ToResponse(PlanPricesView prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    private static PlanPricesView? ToView(PlanPricesRequest? prices) =>
        prices is null ? null : new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    private static IReadOnlyList<MeterSettingView>? ToViews(IReadOnlyList<MeterSettingRequest>? meters) =>
        meters is null ? null : [.. meters.Select(meter => new MeterSettingView(meter.Key, meter.Quota, ResponseStatus.MeterOverageOf(meter.Overage), meter.UnitPrice, meter.UnitSize))];

    private static TenantPlanResponse ToResponse(TenantPlanView plan) =>
        new(plan.TenantId, new PlanNameResponse(plan.Plan.Id, plan.Plan.Name), [.. plan.Entitlements.Select(entitlement => new TenantEntitlementResponse(
            entitlement.Key,
            entitlement.IsFeature,
            entitlement.PlanValue is { } planValue ? ToResponse(planValue) : null,
            entitlement.Override is { } entry
                ? new EntitlementOverrideResponse(
                    ToResponse(entry.Value),
                    entry.ExpiresAt,
                    entry.Expired ? EntitlementOverrideResponseState.Expired : EntitlementOverrideResponseState.Active,
                    entry.Reason,
                    entry.SetById,
                    new UserNameResponse(entry.SetBy.Name, entry.SetBy.Deleted),
                    entry.SetAt)
                : null,
            ToResponse(entitlement.Effective),
            entitlement.Count))], TrialResponse.Of(plan.Trial));
}
