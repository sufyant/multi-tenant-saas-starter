using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Usage;

// The four notices of a meter's use (13.22 to 13.24, 13.43; E10): 80% and all of a meter's quota, and 80% and all of the spending cap. Each
// goes once in each month, in the tenant's bell and by email, to the members who manage billing and to the billing email. A use is a count,
// not a person's change, so no notice names an actor. The meter here: a quota of 100, and 1 € for each unit over it.
[FixedClock("Nothing these tests run schedules a message; the fixed date puts each use in a known month.")]
public sealed class UsageNoticeTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private const string Accounts = "accounts@northwind.test";

    private readonly TestData _testData = new(database);

    private readonly NotificationRows _rows = new(database);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: BillingRequests.On,
            time: _time,
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task UseMeter_To80PercentOfTheQuota_TellsTheMembersWhoManageBillingOnce()
    {
        var (tenant, people) = await TenantAsync("Charge");

        await UseAsync(tenant, 79);
        await UseAsync(tenant, 1);
        await UseAsync(tenant, 5);

        (await NoticesAsync(tenant, people.Owner, people.Manager, people.Member)).ShouldBe(
        [
            NotificationRows.Row(people.Owner, "tenant.meter_nearly_used", tenant, "Tenant"),
            NotificationRows.Row(people.Manager, "tenant.meter_nearly_used", tenant, "Tenant"),
        ], ignoreOrder: true);
        (await _rows.ValueListOfAsync(people.Manager, "tenant.meter_nearly_used")).ShouldBe(
            "meter=ai.requests, quota=100, resetsAt=2026-12-01, tenantName=Northwind Traders, unitPrice=100 EUR, unitSize=1, used=80");
    }

    // One use that takes the count from under 80% to the quota tells of the quota only.
    [Fact]
    public async Task UseMeter_ToTheQuota_TellsOfTheQuotaOnlyAndOnce()
    {
        var (tenant, people) = await TenantAsync("Charge");

        await UseAsync(tenant, 100);
        await UseAsync(tenant, 3);

        (await NoticesAsync(tenant, people.Manager)).ShouldBe([NotificationRows.Row(people.Manager, "tenant.meter_used_up", tenant, "Tenant")]);
        (await _rows.ValueListOfAsync(people.Manager, "tenant.meter_used_up")).ShouldBe(
            "meter=ai.requests, quota=100, resetsAt=2026-12-01, tenantName=Northwind Traders, unitPrice=100 EUR, unitSize=1");
        _api.Email.Sent.Where(sent => sent.To == people.ManagerEmail).Select(sent => sent.Subject).ShouldBe(["Northwind Traders used all its ai.requests"]);
    }

    // A stop meter has no price for its extra: the text says the uses stop until the reset.
    [Fact]
    public async Task UseMeter_AStopMeterToItsQuota_TellsThatItStops()
    {
        var (tenant, people) = await TenantAsync("Stop");

        await UseAsync(tenant, 100);

        (await _rows.ValueListOfAsync(people.Manager, "tenant.meter_used_up")).ShouldBe(
            "meter=ai.requests, quota=100, resetsAt=2026-12-01, tenantName=Northwind Traders");
        _api.Email.Sent.Where(sent => sent.To == people.ManagerEmail).Select(sent => sent.Text).ShouldHaveSingleItem()
            .ShouldContain("They stop until they reset on 1 Dec 2026.");
    }

    [Fact]
    public async Task UseMeter_ToTheQuota_MailsTheBillingEmail()
    {
        var (tenant, people) = await TenantAsync("Charge");
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(people.ManagerExternalId).SetBillingEmailAsync(tenant, Accounts));

        await UseAsync(tenant, 100);

        _api.Email.Sent.Where(sent => sent.To == Accounts).Select(sent => sent.Subject).ShouldBe(["Northwind Traders used all its ai.requests"]);
    }

    // The cap: 500 minor units of extra, so 80% is 400, an extra of 4 units.
    [Fact]
    public async Task UseMeter_To80PercentOfTheCapThenTheCap_TellsOfEachOnce()
    {
        var (tenant, people) = await TenantAsync("Charge", cap: 500);
        await UseAsync(tenant, 100);

        await UseAsync(tenant, 4);
        await UseAsync(tenant, 1);
        await UseAsync(tenant, 1);
        await UseAsync(tenant, 1);

        (await NoticesAsync(tenant, people.Manager)).ShouldBe(
        [
            NotificationRows.Row(people.Manager, "tenant.meter_used_up", tenant, "Tenant"),
            NotificationRows.Row(people.Manager, "tenant.spending_cap_nearly_reached", tenant, "Tenant"),
            NotificationRows.Row(people.Manager, "tenant.spending_cap_reached", tenant, "Tenant"),
        ]);
        (await _rows.ValueListOfAsync(people.Manager, "tenant.spending_cap_nearly_reached")).ShouldBe(
            "cap=500 EUR, extraAmount=400 EUR, resetsAt=2026-12-01, tenantName=Northwind Traders");
        (await _rows.ValueListOfAsync(people.Manager, "tenant.spending_cap_reached")).ShouldBe(
            "cap=500 EUR, meter=ai.requests, resetsAt=2026-12-01, tenantName=Northwind Traders");
    }

    // A changed cap decides again, so its notices may go again in the month.
    [Fact]
    public async Task UseMeter_AfterTheCapChanged_TellsOfTheNewCap()
    {
        var (tenant, people) = await TenantAsync("Charge", cap: 500);
        await UseAsync(tenant, 105);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(people.ManagerExternalId).SetSpendingCapAsync(tenant, 1000));

        await UseAsync(tenant, 5);

        (await _rows.CountAboutAsync(tenant, "tenant.spending_cap_reached")).ShouldBe(4);
    }

    private async Task<(Guid TenantId, People People)> TenantAsync(string overage, long? cap = null)
    {
        var planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        await _testData.SetPlanMeterAsync(planId, Meter, 100, overage, unitPrice: 100, unitSize: 1);
        var tenant = (await _testData.AddTenantAsync(planId: planId, name: "Northwind Traders")).Id;
        var owner = await _testData.AddMemberAsync(tenant, role: "Owner");
        var managerEmail = TestEmails.Of($"manager-{Guid.NewGuid():N}@northwind.test");
        var manager = await _testData.AddMemberAsync(tenant, await _testData.AddUserAsync(email: managerEmail), role: "BillingManager");
        var member = await _testData.AddMemberAsync(tenant);
        await _api.PayAsync(owner, tenant, planId);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(manager).SetSpendingCapAsync(tenant, cap));
        return (tenant, new People(
            await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(manager), manager, managerEmail, await _testData.UserIdOfAsync(member)));
    }

    // The notices of the use only: the payment that the cap needs tells of itself too.
    private async Task<string[]> NoticesAsync(Guid tenantId, params Guid[] userIds) =>
        [.. (await _rows.AboutAsync(tenantId, userIds)).Where(row => row.Contains("|tenant.meter_", StringComparison.Ordinal) || row.Contains("|tenant.spending_cap_", StringComparison.Ordinal))];

    private Task<MeterUse> UseAsync(Guid tenantId, long quantity) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, Meter, quantity, Cancellation);
        });

    private sealed record People(Guid Owner, Guid Manager, string ManagerExternalId, string ManagerEmail, Guid Member);
}
