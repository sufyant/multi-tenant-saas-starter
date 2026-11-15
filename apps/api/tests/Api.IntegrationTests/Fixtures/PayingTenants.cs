using Tenancy;

namespace Api.IntegrationTests.Fixtures;

// The plan changes' setup (B9): two priced plans, Team (EUR 10 per person a month) and Business (EUR 99 base and EUR 8 per person a
// month), and a tenant whose Owner paid for one of them by checkout, with a Billing manager, who takes no seat, to change it.
internal sealed class PayingTenants(Database database, ApiFactory api)
{
    public static readonly DateTimeOffset PeriodEnd = new(2026, 12, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly TestData _testData = new(database);

    private readonly Dictionary<Guid, string> _owners = [];

    public Guid Team { get; private set; }

    public Guid Business { get; private set; }

    public async Task AddPlansAsync()
    {
        Team = await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }, name: $"Team {Guid.NewGuid():N}");
        await _testData.SetPlanPricesAsync(Team, 0, 1000, 0, 10000);
        Business = await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }, name: $"Business {Guid.NewGuid():N}");
        await _testData.SetPlanPricesAsync(Business, 9900, 800, 99000, 8000);
    }

    public async Task<(Guid TenantId, string Manager)> PayingAsync(Guid planId)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        await api.PayAsync(owner, tenant.Id, planId);
        _owners[tenant.Id] = owner;
        return (tenant.Id, manager);
    }

    // The Owner who paid for the tenant's plan.
    public string OwnerOf(Guid tenantId) => _owners[tenantId];

    public Task<Guid> DefaultPlanAsync() => database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans");

    public Task<Guid> PlanOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT plan_id FROM control_plane.tenants WHERE id = '{tenantId}'");

    // The row's plan, interval, end and scheduled move.
    public Task<string?> SubscriptionOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', plan_id, interval, cancel_at_period_end, coalesce(scheduled_plan_id::text, ''), coalesce(scheduled_interval, '')) FROM billing.subscriptions WHERE tenant_id = '{tenantId}'");
}
