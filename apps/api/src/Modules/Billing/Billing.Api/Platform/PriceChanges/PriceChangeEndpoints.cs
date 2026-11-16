using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Api.Tenant.Subscriptions;
using Billing.Application.PriceChanges.GetPriceChange;
using Billing.Application.PriceChanges.GetPriceChangeFacts;
using Billing.Application.PriceChanges.MoveToNewPrice;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Platform.PriceChanges;

/// <summary>
/// A plan's price change for the system admins (B13, 6.14 to 6.16): who pays the plan by card, who pays an old price, and the move of them
/// to the new price after 30 days' notice. A plan belongs to no tenant, so the messages declare none.
/// </summary>
public static class PriceChangeEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/plans/{planId:guid}/price-change", GetPriceChange).RequireAuthorization(BillingPermissions.PlatformPlansRead);
        platform.MapPost("/plans/{planId:guid}/price-change/move", MoveToNewPrice).RequireAuthorization(BillingPermissions.PlatformPlansUpdate);
    }

    // ControlPlane's plan and its prices of today, then billing's payers and changes, each on its module's DbContext (W1).
    private static async Task<Result<PriceChangeResponse>> GetPriceChange(Guid planId, ClaimsPrincipal user, BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (await bus.InvokeAsync<PriceChangeFacts?>(new GetPriceChangeFactsQuery(user.Id(), planId), cancellationToken) is not { } facts)
        {
            return Errors.PlanNotFound;
        }

        return await ReadAsync(bus, facts, cancellationToken);
    }

    // It sets a state: a move scheduled already keeps its day and answers 200, so it takes no idempotency key (I1).
    private static async Task<Result<PriceChangeResponse>> MoveToNewPrice(Guid planId, ClaimsPrincipal user, BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (await bus.InvokeAsync<PriceChangeFacts?>(new GetPriceChangeFactsQuery(user.Id(), planId), cancellationToken) is not { } facts)
        {
            return Errors.PlanNotFound;
        }

        var moved = await bus.InvokeAsync<Result>(new MoveToNewPriceCommand(facts.ActorId, planId), cancellationToken);
        if (!moved.IsSuccess)
        {
            return moved.Error;
        }

        return await ReadAsync(bus, facts, cancellationToken);
    }

    private static async Task<PriceChangeResponse> ReadAsync(IMessageBus bus, PriceChangeFacts facts, CancellationToken cancellationToken)
    {
        var view = await bus.InvokeAsync<PriceChangeView>(new GetPriceChangeQuery(facts.PlanId, facts.Prices), cancellationToken);
        return new PriceChangeResponse(
            view.Payers,
            view.YearlyPayers,
            view.OnOldPrice is { } old ? new OldPriceResponse(old.Count, old.OldPrices is { } before ? PricesResponse.Of(before) : null, old.Since) : null,
            view.MovesAt);
    }
}
