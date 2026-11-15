using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.Provider;
using Billing.Application.Subscriptions.BeginPlanChange;
using Billing.Application.Subscriptions.CancelAtPeriodEnd;
using Billing.Application.Subscriptions.ChangePlanNow;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions.GetPlanChangePreview;
using Billing.Application.Subscriptions.SchedulePlanChange;
using Billing.Application.Subscriptions.SyncSubscription;
using Billing.Application.Subscriptions.UndoScheduledChange;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// A member who manages billing moves the tenant's subscription to another plan or interval, cancels it, or undoes a scheduled move or
/// cancellation (B9, 10.02 to 10.07).
/// </summary>
public static class PlanChangeEndpoints
{
    // A preview older than this charges another amount than it showed, so the move asks for a new one.
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapPost("/billing/plan-change/preview", PreviewPlanChange).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());
        tenant.MapPost("/billing/plan-change", ChangePlan).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());
        tenant.MapDelete("/billing/scheduled-change", UndoScheduledChange).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());
    }

    // ControlPlane's facts and billing's own rows are messages on their module's DbContext (W1); the provider's call is a message with no
    // transaction open. The preview writes nothing.
    private static async Task<Result<PlanChangePreviewResponse>> PreviewPlanChange(
        PlanChangePreviewRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
        IMessageBus bus,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var change = await PlanChangeOfAsync(billing, bus, user, request.PlanId, request.Interval, cancellationToken);
        if (!change.IsSuccess)
        {
            return change.Error;
        }

        var move = MoveOf(change.Value);
        if (!change.Value.IsNow)
        {
            return new PlanChangePreviewResponse(move, change.Value.At, change.Value.Order.Currency, change.Value.Amount, null, null);
        }

        var now = time.GetUtcNow();
        var preview = await bus.InvokeAsync<PlanChangePreview>(new GetPlanChangePreviewQuery(change.Value.Order, now), cancellationToken);
        return new PlanChangePreviewResponse(move, now, change.Value.Order.Currency, change.Value.Amount, preview.AmountDue, preview.ProratedAt);
    }

    // The request sets a state at the provider: the same move again finds it made (409 billing.plan_unchanged), so it takes no
    // idempotency key (I1). The tenant's plan changes when the provider's event of the change arrives, as after a checkout (B6); the endpoint
    // asks for the sync at once too, so the page need not wait for the webhook.
    private static async Task<Result<PlanChangeResponse>> ChangePlan(
        PlanChangeRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
        IMessageBus bus,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var change = await PlanChangeOfAsync(billing, bus, user, request.PlanId, request.Interval, cancellationToken);
        if (!change.IsSuccess)
        {
            return change.Error;
        }

        var now = time.GetUtcNow();
        var proratedAt = request.ProratedAt ?? now;
        if (change.Value.IsNow && (proratedAt > now.AddMinutes(1) || proratedAt < now - PreviewLifetime))
        {
            return Errors.PreviewExpired;
        }

        var begun = await bus.InvokeAsync<Result<SubscriptionView>>(new BeginPlanChangeCommand(change.Value.PaidBy, change.Value.IsCancel), cancellationToken);
        if (!begun.IsSuccess)
        {
            return begun.Error;
        }

        var order = change.Value.Order with { SubscriptionId = begun.Value.SubscriptionId };
        if (change.Value.IsNow)
        {
            if (!await bus.InvokeAsync<bool>(new ChangePlanNowCommand(order, proratedAt), cancellationToken))
            {
                return Errors.PaymentDeclined;
            }
        }
        else if (change.Value.IsCancel)
        {
            await bus.InvokeAsync(new CancelAtPeriodEndCommand(order.SubscriptionId), cancellationToken);
        }
        else
        {
            await bus.InvokeAsync(new SchedulePlanChangeCommand(order), cancellationToken);
        }

        await bus.PublishAsync(new SyncSubscriptionCommand(order.SubscriptionId));
        return new PlanChangeResponse(MoveOf(change.Value), change.Value.IsNow ? now : begun.Value.CurrentPeriodEnd);
    }

    // The undo of the move or the end that a member scheduled at the period's end; the closure's end is not a member's to undo (B8).
    private static async Task<Result> UndoScheduledChange(ClaimsPrincipal user, BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (await bus.InvokeAsync<SubscriptionView?>(new GetBillingQuery(), cancellationToken) is not { } subscription)
        {
            return Errors.NoSubscription;
        }

        var facts = await bus.InvokeAsync<Result<CheckoutFacts>>(new GetCheckoutFactsQuery(user.Id(), subscription.PlanId), cancellationToken);
        if (!facts.IsSuccess)
        {
            return facts.Error;
        }

        var begun = await bus.InvokeAsync<Result<SubscriptionView>>(new BeginPlanChangeCommand(facts.Value.PaidBy, ByMemberEnd: true, Undo: true), cancellationToken);
        if (!begun.IsSuccess)
        {
            return begun.Error;
        }

        await bus.InvokeAsync(new UndoScheduledChangeCommand(begun.Value.SubscriptionId), cancellationToken);
        await bus.PublishAsync(new SyncSubscriptionCommand(begun.Value.SubscriptionId));
        return Result.Success();
    }

    private static async Task<Result<PlanChange>> PlanChangeOfAsync(
        BillingOptions billing,
        IMessageBus bus,
        ClaimsPrincipal user,
        Guid planId,
        PlanChangeRequestInterval interval,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        var target = await bus.InvokeAsync<Result<CheckoutFacts>>(new GetCheckoutFactsQuery(user.Id(), planId), cancellationToken);
        if (!target.IsSuccess)
        {
            return target.Error;
        }

        if (await bus.InvokeAsync<SubscriptionView?>(new GetBillingQuery(), cancellationToken) is not { } subscription)
        {
            return Errors.NoSubscription;
        }

        var current = await bus.InvokeAsync<Result<CheckoutFacts>>(new GetCheckoutFactsQuery(user.Id(), subscription.PlanId), cancellationToken);
        if (!current.IsSuccess)
        {
            return current.Error;
        }

        return PlanChanges.Of(subscription, current.Value, target.Value, interval.ToString());
    }

    private static PlanChangeResponseMove MoveOf(PlanChange change) =>
        change.IsNow ? PlanChangeResponseMove.Now : change.IsCancel ? PlanChangeResponseMove.Cancellation : PlanChangeResponseMove.AtPeriodEnd;
}
