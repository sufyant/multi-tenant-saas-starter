using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Fixtures;

// A price change's setup (B13): a host whose billing is on, with a clock the test moves, the two priced plans of PayingTenants, and
// tenants that pay for one of them, whose subscription at the fake names what it pays, so billing knows it. Team's new price is EUR 12 per
// person a month and EUR 120 a year, the base fee stays 0 (6.14).
[FixedClock("The clock starts at the real now and moves only forward, to the moment of each message that a test delivers itself; what the host schedules lies 30 days or more after the real now, past any run.")]
internal sealed class PricedPlans : IAsyncDisposable
{
    public static readonly object NewTeamPrices = new { baseMonthly = 0, perPersonMonthly = 1_200, baseYearly = 0, perPersonYearly = 12_000 };

    private readonly Database _database;

    private PricedPlans(Database database, FakeTimeProvider time, ApiFactory api)
    {
        _database = database;
        Time = time;
        Api = api;
        Tenants = new PayingTenants(database, api);
    }

    public FakeTimeProvider Time { get; }

    public ApiFactory Api { get; }

    public PayingTenants Tenants { get; }

    public TestData TestData => new(_database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static async Task<PricedPlans> StartAsync(Database database)
    {
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));
        var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: time, settings: BillingRequests.On);
        var changes = new PricedPlans(database, time, api);
        await changes.Tenants.AddPlansAsync();
        return changes;
    }

    // A tenant that pays for the plan by checkout, then Stripe's state of what it pays: Team's today's prices, monthly or yearly.
    public async Task<(Guid TenantId, string Manager)> PayingAsync(Guid planId, bool yearly = false)
    {
        var paying = await Tenants.PayingAsync(planId);
        Api.Billing.Put(BillingRequests.Subscription(paying.TenantId, planId) with
        {
            Yearly = yearly,
            BaseAmount = 0,
            PerPersonAmount = yearly ? 10_000 : 1_000,
        });
        await Api.WaitingForMessagesAsync(() => Api.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.updated", $"sub_{paying.TenantId:N}"));
        return paying;
    }

    public async Task<HttpClient> AdminAsync() => Api.CreateClient(await TestData.AddSystemAdminAsync(), secondFactor: true);

    // The platform's edit of Team's prices (6.14), with the answer to "Who pays the new price, and when?".
    public async Task<HttpResponseMessage> ChangeTeamPriceAsync(object priceChange)
    {
        var admin = await AdminAsync();
        var name = await _database.ScalarAsSuperuserAsync<string>($"SELECT name FROM control_plane.plans WHERE id = '{Tenants.Team}'");
        var changed = await admin.PatchAsJsonAsync(
            $"/v1/platform/plans/{Tenants.Team}",
            new { name, isDefault = false, entitlements = new Dictionary<string, object> { ["members.max"] = PlanRequests.Unlimited }, prices = NewTeamPrices, priceChange },
            Cancellation);
        changed.EnsureSuccessStatusCode();
        return changed;
    }

    // Each scheduled message of the session, delivered at its moment, as Wolverine does when the moment comes.
    public async Task DeliverScheduledAsync(ITrackedSession session)
    {
        foreach (var envelope in session.Scheduled.Envelopes().OrderBy(envelope => envelope.ScheduledTime))
        {
            Time.SetUtcNow(envelope.ScheduledTime!.Value);
            await Api.WaitingForMessagesAsync(async () =>
            {
                using var scope = Api.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(envelope.Message!, Cancellation);
                return envelope;
            });
        }
    }

    public Task<long> AuditCountAsync(Guid tenantId, string operation) =>
        _database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = '{operation}' AND actor_kind = 'Platform'");

    public Task<long> PlatformAuditCountAsync(Guid planId, string operation) =>
        _database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{planId}' AND operation = '{operation}'");

    public ValueTask DisposeAsync() => Api.DisposeAsync();
}
