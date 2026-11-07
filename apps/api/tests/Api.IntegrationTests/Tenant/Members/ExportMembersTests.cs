using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Members;

// POST /v1/tenants/{tenantId}/members/export: the web's server calls it before it writes the members' CSV file, so the tenant's Activity
// log records who exported them (K3; the maintainer's decision of 2026-11-06). It needs the permission of the list it exports.
public sealed class ExportMembersTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // Every built-in role reads the members, so every one may export them. The entry is about the tenant and keeps its name; it keeps no
    // search or filter, which may hold a person's name or address (K3).
    [Theory]
    [InlineData("Owner")]
    [InlineData("Member")]
    public async Task ExportMembers_AMemberWhoReadsTheMembers_IsNoContentAndRecordsMembersExported(string role)
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberAsync(tenant.Id, role: role);

        var exported = await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor).PostAsync($"/v1/tenants/{tenant.Id}/members/export", null, Cancellation));

        exported.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:members.exported|User|{await _testData.UserIdOfAsync(actor)}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe("-");
    }

    [Fact]
    public async Task ExportMembers_ARoleWithoutTheMembersRead_IsForbiddenAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberWithRoleAsync(tenant.Id, await _testData.AddCustomRoleAsync(tenant.Id, "Viewer", "tenant.profile.read"));

        var exported = await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor).PostAsync($"/v1/tenants/{tenant.Id}/members/export", null, Cancellation));

        exported.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
    }
}
