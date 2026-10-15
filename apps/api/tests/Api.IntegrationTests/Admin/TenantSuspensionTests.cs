using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// A system admin suspends and reactivates a tenant through the system door. A change of status is announced, and Audit records it
// under the tenant; a request that changes nothing announces nothing. SuspendedTenantTests follows what a suspended tenant means to
// its members.
public sealed class TenantSuspensionTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Active", "suspend", "Suspended", "suspended")]
    [InlineData("Active", "reactivate", "Active", "active")]
    [InlineData("Suspended", "suspend", "Suspended", "suspended")]
    [InlineData("Suspended", "reactivate", "Active", "active")]
    public async Task SuspendOrReactivateTenant_ActiveOrSuspended_AnswersTheTenantInItsNewStatus(string status, string action, string stored, string answered)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).PostAsync($"/v1/admin/tenants/{tenant.Id}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(tenant.Id);
        body.GetProperty("slug").GetString().ShouldBe(tenant.Slug);
        body.GetProperty("status").GetString().ShouldBe(answered);
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(stored);
    }

    // A tenant whose creation has not passed its pivot, or never will, is neither suspended nor reactivated.
    [Theory]
    [InlineData("Provisioning", "suspend")]
    [InlineData("Provisioning", "reactivate")]
    [InlineData("Cancelled", "suspend")]
    [InlineData("Cancelled", "reactivate")]
    public async Task SuspendOrReactivateTenant_ProvisioningOrCancelled_IsAConflictAndChangesNothing(string status, string action)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).PostAsync($"/v1/admin/tenants/{tenant.Id}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_active");
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task SuspendOrReactivateTenant_UnknownTenant_IsNotFound(string action)
    {
        var response = await (await AdminAsync()).PostAsync($"/v1/admin/tenants/{Guid.NewGuid()}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_found");
    }

    // K3: the platform's log names the system admin; the tenant's own log says only that the platform acted.
    [Fact]
    public async Task SuspendAndReactivateTenant_EachChange_IsRecordedOnceInBothLogs()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddSystemAdminAsync();
        var client = _api.CreateClient(admin, secondFactor: true);

        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/admin/tenants/{tenant.Id}/suspend", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/admin/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var adminId = await _catalog.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe(
            $"tenant.suspended|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd, tenant.reactivated|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe(
            $"{tenant.Id}:tenant.suspended|Platform|-|Tenant|{tenant.Id}|Acme Ltd, {tenant.Id}:tenant.reactivated|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
    }

    [Fact]
    public async Task SuspendTenant_Twice_IsRecordedOnceInEachLog()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/admin/tenants/{tenant.Id}/suspend", null, Cancellation));

        var again = await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/admin/tenants/{tenant.Id}/suspend", null, Cancellation));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task ReactivateTenant_AlreadyActive_IsNotRecorded()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await AdminAsync()).PostAsync($"/v1/admin/tenants/{tenant.Id}/reactivate", null, Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(0);
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
