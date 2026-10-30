using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Notifications;

// POST /v1/tenants/{tenantId}/notifications/read (U7): marks read everything the tenant's bell shows, this tenant's and the personal ones,
// and nothing of another tenant. GET .../unread-count counts what the bell shows unread.
public sealed class MarkAllNotificationsReadTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly NotificationRows _rows = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task MarkAllNotificationsRead_OneTenantsBell_MarksItsOwnAndThePersonalOnesAndLeavesAnotherTenantsUnread()
    {
        var (tenant, other) = (await _testData.AddTenantAsync(), await _testData.AddTenantAsync());
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(other.Id, member);
        var userId = await _testData.UserIdOfAsync(member);
        var own = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant");
        var personal = await _rows.AddAsync(userId, "member.removed", null, "Personal");
        var elsewhere = await _rows.AddAsync(userId, "tenant.plan_changed", other.Id, "Tenant");
        var client = _api.CreateClient(member);
        var before = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications/unread-count", Cancellation);

        var marked = await client.PostAsync($"/v1/tenants/{tenant.Id}/notifications/read", null, Cancellation);

        (marked.StatusCode, before.GetProperty("unread").GetInt32()).ShouldBe((HttpStatusCode.NoContent, 2));
        (await _rows.IsReadAsync(own), await _rows.IsReadAsync(personal), await _rows.IsReadAsync(elsewhere)).ShouldBe((true, true, false));
        (await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{other.Id}/notifications/unread-count", Cancellation)).GetProperty("unread").GetInt32().ShouldBe(1);
    }
}
