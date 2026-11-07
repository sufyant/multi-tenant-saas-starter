using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Tenant.AuditLogs;

// POST /v1/tenants/{tenantId}/audit-log/export: the web's server calls it before it writes the Activity log's CSV file, so the log
// records who exported it (K3; the maintainer's decision of 2026-11-06). It needs the permission of the log it exports.
public sealed class ExportAuditLogTests(Database database) : IAsyncLifetime
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

    // Audit knows the tenant by its id only, so the entry is about the tenant without its name.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public async Task ExportAuditLog_AMemberWhoReadsTheLog_IsNoContentAndRecordsActivityLogExported(string role)
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberAsync(tenant.Id, role: role);

        var exported = await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor).PostAsync($"/v1/tenants/{tenant.Id}/audit-log/export", null, Cancellation));

        exported.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:activity_log.exported|User|{await _testData.UserIdOfAsync(actor)}|Tenant|{tenant.Id}|-");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe("-");
    }

    [Fact]
    public async Task ExportAuditLog_AMember_IsForbiddenAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var actor = await _testData.AddMemberAsync(tenant.Id, role: "Member");

        var exported = await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor).PostAsync($"/v1/tenants/{tenant.Id}/audit-log/export", null, Cancellation));

        exported.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
    }
}
