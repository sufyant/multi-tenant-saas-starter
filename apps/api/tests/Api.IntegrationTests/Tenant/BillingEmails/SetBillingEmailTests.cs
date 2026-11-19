using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.BillingEmails;

// PUT /v1/tenants/{tenantId}/billing/email (10.01, B14): a member who manages billing sets the tenant's optional billing email, an address
// with no account. Stripe's invoices go to it as the customer's email, and billing's payment emails go to it besides the members who
// manage billing, in the language of the member who set it.
public sealed class SetBillingEmailTests(Database database) : IAsyncLifetime
{
    private static readonly string Accounts = PlatformTenants.Email();

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private Guid _planId;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: BillingRequests.On);
        _planId = await _testData.AddPlanAsync([]);
        await _testData.SetPlanPricesAsync(_planId, 900, 1000, 9000, 10000);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SetBillingEmail_AnAddress_IsTheBillingEmailOfTheRead()
    {
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        var client = _api.CreateClient(manager);

        var set = await client.SetBillingEmailAsync(tenant.Id, $"  {Accounts} ");

        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await BillingEmailAsync(client, tenant.Id)).ShouldBe(Accounts);
        _api.Billing.CustomerEmails.ShouldBeEmpty();
    }

    // Stripe sends its invoices and receipts to the customer's one address (Stripe, "Customer emails").
    [Fact]
    public async Task SetBillingEmail_OfAPayingTenant_MakesItStripesCustomerEmail()
    {
        var tenant = await _testData.AddTenantAsync();
        var payer = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: payer), role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).SetBillingEmailAsync(tenant.Id, Accounts));

        _api.Billing.CustomerEmails.ShouldBe([(FakeBillingProvider.CustomerOf(tenant.Id), payer), (FakeBillingProvider.CustomerOf(tenant.Id), Accounts)]);
    }

    // Without a billing email, Stripe's invoices go back to the member who paid first.
    [Fact]
    public async Task SetBillingEmail_None_RemovesItAndGivesStripeThePayersAddressBack()
    {
        var tenant = await _testData.AddTenantAsync();
        var payer = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: payer), role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        var client = _api.CreateClient(owner);
        await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, Accounts));

        var removed = await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, ""));

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await BillingEmailAsync(client, tenant.Id)).ShouldBeNull();
        _api.Billing.CustomerEmails.Last().ShouldBe((FakeBillingProvider.CustomerOf(tenant.Id), payer));
    }

    // The same address again changes nothing and asks nothing of Stripe (I1).
    [Fact]
    public async Task SetBillingEmail_TheSameAddressAgain_AsksNothingOfStripe()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.PayAsync(owner, tenant.Id, _planId);
        var client = _api.CreateClient(owner);
        await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, Accounts));

        await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, Accounts.ToUpperInvariant()));

        _api.Billing.CustomerEmails.Count(email => string.Equals(email.Email, Accounts, StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    // K3, 12.12: the tenant's log records the change by the member, without the address.
    [Fact]
    public async Task SetBillingEmail_AnAddressThenNone_RecordsBothInTheTenantsLogWithoutTheAddress()
    {
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");
        var client = _api.CreateClient(manager);
        await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, Accounts));

        await _api.WaitingForMessagesAsync(() => client.SetBillingEmailAsync(tenant.Id, null));

        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(concat_ws('|', actor_kind, actor_id = '{await _testData.UserIdOfAsync(manager)}', details->>'removed', details::text LIKE '%@%'), ',' ORDER BY occurred_at) FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.billing_email_changed'"))
            .ShouldBe("User|t|false|f,User|t|true|f");
    }

    [Fact]
    public async Task SetBillingEmail_AnAddressOverItsLimit_IsRefusedWithItsFieldError()
    {
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");

        var set = await _api.CreateClient(manager).SetBillingEmailAsync(tenant.Id, $"{new string('a', 250)}@app.test");

        set.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await set.Content.ReadAsStringAsync(Cancellation)).ShouldContain("too_long");
    }

    [Fact]
    public async Task SetBillingEmail_AnAddressThatIsNoEmail_IsRefusedWithItsFieldError()
    {
        var tenant = await _testData.AddTenantAsync();
        var manager = await _testData.AddMemberAsync(tenant.Id, role: "BillingManager");

        var set = await _api.CreateClient(manager).SetBillingEmailAsync(tenant.Id, "accounts");

        set.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await set.Content.ReadAsStringAsync(Cancellation)).ShouldContain("email_invalid");
    }

    // A1: only a role that holds tenant.billing.manage sets it.
    [Fact]
    public async Task SetBillingEmail_AMember_IsRefused()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);

        var set = await _api.CreateClient(member).SetBillingEmailAsync(tenant.Id, Accounts);

        set.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // B1: while billing is off, the endpoint answers 404 billing.off.
    [Fact]
    public async Task SetBillingEmail_BillingOff_AnswersNotFound()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var set = await off.CreateClient(owner).SetBillingEmailAsync(tenant.Id, Accounts);

        (set.StatusCode, await BillingRequests.CodeOfAsync(set)).ShouldBe((HttpStatusCode.NotFound, "billing.off"));
    }

    private static async Task<string?> BillingEmailAsync(HttpClient client, Guid tenantId)
    {
        var billing = await (await client.GetBillingAsync(tenantId)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return billing.GetProperty("billingEmail").GetString();
    }
}
