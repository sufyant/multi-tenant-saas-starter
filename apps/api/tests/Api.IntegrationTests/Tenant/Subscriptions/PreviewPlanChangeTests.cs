using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Subscriptions;

// POST /v1/tenants/{tenantId}/billing/plan-change/preview (B9, 10.04): what a move does before the member confirms it, and what the rest
// of the period costs today for a move at once. It writes nothing and moves nothing.
public sealed class PreviewPlanChangeTests(Database database) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    private PayingTenants _tenants = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _tenants = new PayingTenants(database, _api);
        await _tenants.AddPlansAsync();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task PreviewPlanChange_AnUpgrade_AnswersTheRestOfThePeriodAndTheNewAmount()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Team);

        var answer = await _api.CreateClient(manager).PreviewPlanChangeAsync(tenantId, _tenants.Business);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (preview.GetProperty("move").GetString(), preview.GetProperty("currency").GetString(), preview.GetProperty("amount").GetInt64(), preview.GetProperty("amountToday").GetInt64())
            .ShouldBe(("now", "EUR", 10700L, 5325L));
        _api.Billing.MovesNow.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewPlanChange_ACheaperPlan_AnswersTheEndOfThePeriodAndNothingToday()
    {
        var (tenantId, manager) = await _tenants.PayingAsync(_tenants.Business);

        var preview = await (await _api.CreateClient(manager).PreviewPlanChangeAsync(tenantId, _tenants.Team)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        (preview.GetProperty("move").GetString(), preview.GetProperty("at").GetDateTimeOffset(), preview.GetProperty("amount").GetInt64(), preview.GetProperty("amountToday").ValueKind)
            .ShouldBe(("at_period_end", PayingTenants.PeriodEnd, 1000L, JsonValueKind.Null));
        _api.Billing.Scheduled.ShouldBeEmpty();
    }
}
