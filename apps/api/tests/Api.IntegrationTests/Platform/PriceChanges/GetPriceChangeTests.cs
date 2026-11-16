using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.PriceChanges;

// B13, 6.14 and 6.15: the platform reads who pays for a plan by card, and who pays a price the plan no longer has.
public sealed class GetPriceChangeTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // 6.14: "Affects 5 organisations. 2 on yearly plans keep their price until renewal."
    [Fact]
    public async Task GetPriceChange_PayersOfThePlan_CountsThemByInterval()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        await billing.PayingAsync(billing.Tenants.Team, yearly: true);
        var admin = await billing.AdminAsync();

        var read = await admin.GetAsync($"/v1/platform/plans/{billing.Tenants.Team}/price-change", Cancellation);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var answer = await read.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (answer.GetProperty("payers").GetInt32(), answer.GetProperty("yearlyPayers").GetInt32(), answer.GetProperty("onOldPrice").ValueKind)
            .ShouldBe((2, 1, JsonValueKind.Null));
    }

    // 6.15: "5 customers on the old price. They pay €10 per person a month. New customers pay €12 per person since 2 Nov 2026."
    [Fact]
    public async Task GetPriceChange_ANewPriceForNewCustomersOnly_AnswersThePayersOnTheOldPrice()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        await billing.PayingAsync(billing.Tenants.Team);
        await billing.Api.WaitingForMessagesAsync(() => billing.ChangeTeamPriceAsync(new { who = "new_customers_only" }));
        var admin = await billing.AdminAsync();

        var answer = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/plans/{billing.Tenants.Team}/price-change", Cancellation);

        var old = answer.GetProperty("onOldPrice");
        (old.GetProperty("count").GetInt32(), old.GetProperty("oldPrices").GetProperty("perPersonMonthly").GetInt64(), old.GetProperty("since").GetDateTimeOffset())
            .ShouldBe((1, 1_000L, billing.Time.GetUtcNow()));
        answer.GetProperty("movesAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetPriceChange_AnUnknownPlan_IsNotFound()
    {
        await using var billing = await PricedPlans.StartAsync(database);
        var admin = await billing.AdminAsync();

        var read = await admin.GetAsync($"/v1/platform/plans/{Guid.NewGuid()}/price-change", Cancellation);

        (read.StatusCode, await BillingRequests.CodeOfAsync(read)).ShouldBe((HttpStatusCode.NotFound, "billing.plan_not_found"));
    }
}
