using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.Application.PriceChanges.MovePlanPrices;

namespace Api.IntegrationTests.Platform.PriceChanges;

// B13, 6.16: "Move 5 customers to €12 per person a month?" The payers on an old price are told today and move after 30 days' notice; a
// Contract tenant and a subscription that ends at its period's end move nowhere (B12).
public sealed class MoveToNewPriceTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MoveToNewPrice_PayersOnTheOldPrice_TellsThemTodayAndMovesThemIn30Days()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var (tenantId, _) = await billing.PayingAsync(billing.Tenants.Team);
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));
        var admin = await billing.AdminAsync();

        var session = await billing.Api.TrackMessagesAsync(() => admin.PostAsync($"/v1/platform/plans/{billing.Tenants.Team}/price-change/move", null, Cancellation));

        var movesAt = billing.Time.GetUtcNow().AddDays(30);
        session.Scheduled.Envelopes().ShouldContain(envelope => envelope.Message is MovePlanPricesCommand && envelope.ScheduledTime == movesAt);
        (await billing.AuditCountAsync(tenantId, "tenant.price_change_announced")).ShouldBe(1);
        var answer = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{billing.Tenants.Team}/price-change", Cancellation);
        answer.GetProperty("movesAt").GetDateTimeOffset().ShouldBe(movesAt);

        await billing.DeliverScheduledAsync(session);

        billing.Api.Billing.PriceMoves.ShouldBe([($"sub_{tenantId:N}", 0L, 1_200L)]);
    }

    // A move scheduled already keeps its day: the same request again schedules nothing more (I1).
    [Fact]
    public async Task MoveToNewPrice_AMoveScheduledAlready_KeepsItsDay()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));
        var admin = await billing.AdminAsync();
        var path = $"/v1/platform/plans/{billing.Tenants.Team}/price-change/move";
        await billing.Api.WaitingForMessagesAsync(() => admin.PostAsync(path, null, Cancellation));
        billing.Time.Advance(TimeSpan.FromDays(1));

        var again = await billing.Api.TrackMessagesAsync(() => admin.PostAsync(path, null, Cancellation));

        again.Scheduled.Envelopes().ShouldNotContain(envelope => envelope.Message is MovePlanPricesCommand);
        var answer = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{billing.Tenants.Team}/price-change", Cancellation);
        answer.GetProperty("movesAt").GetDateTimeOffset().ShouldBe(billing.Time.GetUtcNow().AddDays(29));
    }

    // A move that a member scheduled at the period's end takes its own Prices, so the price change tells it nothing and moves it nowhere (B9).
    [Fact]
    public async Task MoveToNewPrice_ASubscriptionWithAScheduledMove_IsNeitherToldNorMoved()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var (tenantId, _) = await billing.PayingAsync(billing.Tenants.Team);
        billing.Api.Billing.Put(BillingRequests.Subscription(tenantId, billing.Tenants.Team) with
        {
            BaseAmount = 0,
            PerPersonAmount = 1_000,
            ScheduledPlanId = billing.Tenants.Business,
            ScheduledYearly = false,
        });
        await billing.Api.WaitingForMessagesAsync(() => billing.Api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{tenantId:N}"));
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));
        var admin = await billing.AdminAsync();

        var session = await billing.Api.TrackMessagesAsync(() => admin.PostAsync($"/v1/platform/plans/{billing.Tenants.Team}/price-change/move", null, Cancellation));
        await billing.DeliverScheduledAsync(session);

        billing.Api.Billing.PriceMoves.ShouldBeEmpty();
        (await billing.AuditCountAsync(tenantId, "tenant.price_change_announced")).ShouldBe(0);
    }

    [Fact]
    public async Task MoveToNewPrice_NobodyOnAnOldPrice_IsAConflict()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        var admin = await billing.AdminAsync();

        var moved = await admin.PostAsync($"/v1/platform/plans/{billing.Tenants.Team}/price-change/move", null, Cancellation);

        (moved.StatusCode, await BillingRequests.CodeOfAsync(moved)).ShouldBe((HttpStatusCode.Conflict, "billing.nothing_to_move"));
    }

    // A Contract tenant is billed outside the app, and a subscription that ends at its period's end pays no next period (B12).
    [Fact]
    public async Task MoveToNewPrice_AContractTenantAndAnEndingSubscription_MovesNeither()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var (contractId, _) = await billing.PayingAsync(billing.Tenants.Team);
        var (endingId, _) = await billing.PayingAsync(billing.Tenants.Team);
        var (payingId, _) = await billing.PayingAsync(billing.Tenants.Team);
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));
        var admin = await billing.AdminAsync();
        await billing.Api.WaitingForMessagesAsync(() => admin.SetBilledByAsync(contractId, "contract"));
        billing.Api.Billing.Put(BillingRequests.Subscription(endingId, billing.Tenants.Team) with { BaseAmount = 0, PerPersonAmount = 1_000, CancelAtPeriodEnd = true });
        await billing.Api.WaitingForMessagesAsync(() => billing.Api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{endingId:N}"));

        var session = await billing.Api.TrackMessagesAsync(() => admin.PostAsync($"/v1/platform/plans/{billing.Tenants.Team}/price-change/move", null, Cancellation));
        await billing.DeliverScheduledAsync(session);

        billing.Api.Billing.PriceMoves.ShouldBe([($"sub_{payingId:N}", 0L, 1_200L)]);
        (await billing.AuditCountAsync(endingId, "tenant.price_change_announced")).ShouldBe(0);
    }
}
