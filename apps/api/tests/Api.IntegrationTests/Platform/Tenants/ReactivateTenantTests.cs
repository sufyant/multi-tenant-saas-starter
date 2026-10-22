using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Tenants;

// POST /v1/platform/tenants/{tenantId}/reactivate: a system admin reactivates a suspended tenant through the system door. A change of
// status is announced, and Audit records it under the tenant; a request that changes nothing announces nothing. A test of both a
// suspension and a reactivation sits here, with the use case it checks last (N9).
public sealed class ReactivateTenantTests(Database database) : IAsyncLifetime
{
    private const string Reason = "Unpaid invoices";

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

    [Theory]
    [InlineData("Active", "suspend", "Suspended", "suspended")]
    [InlineData("Active", "reactivate", "Active", "active")]
    [InlineData("Suspended", "suspend", "Suspended", "suspended")]
    [InlineData("Suspended", "reactivate", "Active", "active")]
    public async Task SuspendOrReactivateTenant_ActiveOrSuspended_AnswersTheTenantInItsNewStatus(string status, string action, string stored, string answered)
    {
        var tenant = await _testData.AddTenantAsync(status: status);

        var response = await (await SystemAdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{tenant.Id}/{action}", new { reason = Reason }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "suspensionReason", "closedAt"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(tenant.Id);
        body.GetProperty("slug").GetString().ShouldBe(tenant.Slug);
        body.GetProperty("status").GetString().ShouldBe(answered);
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe(stored);
    }

    // A closed tenant is neither suspended nor reactivated: it comes back only through a reopening.
    [Theory]
    [InlineData("Closed", "suspend")]
    [InlineData("Closed", "reactivate")]
    public async Task SuspendOrReactivateTenant_Closed_IsAConflictAndChangesNothing(string status, string action)
    {
        var tenant = await _testData.AddTenantAsync(status: status);

        var response = await (await SystemAdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{tenant.Id}/{action}", new { reason = Reason }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_active");
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task SuspendOrReactivateTenant_UnknownTenant_IsNotFound(string action)
    {
        var response = await (await SystemAdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{Guid.NewGuid()}/{action}", new { reason = Reason }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_found");
    }

    // K3: the platform's log names the system admin; the tenant's own log says only that the platform acted.
    [Fact]
    public async Task SuspendAndReactivateTenant_EachChange_IsRecordedOnceInBothLogs()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddSystemAdminAsync();
        var client = _api.CreateClient(admin, secondFactor: true);

        await _api.WaitingForMessagesAsync(() => client.SuspendTenantAsync(tenant.Id));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe(
            $"tenant.suspended|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd, tenant.reactivated|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe(
            $"{tenant.Id}:tenant.suspended|Platform|-|Tenant|{tenant.Id}|Acme Ltd, {tenant.Id}:tenant.reactivated|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
    }

    [Fact]
    public async Task ReactivateTenant_AlreadyActive_IsNotRecorded()
    {
        var tenant = await _testData.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task ReactivateTenant_Suspended_ForgetsTheReason()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await SystemAdminAsync();
        await admin.SuspendTenantAsync(tenant.Id);

        var response = await admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation);

        (await SuspensionReasonOfAsync(response)).ShouldBeNull();
        (await SuspensionReasonOfAsync(await admin.GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation))).ShouldBeNull();
    }

    private static async Task<string?> SuspensionReasonOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("suspensionReason").GetString();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");
}
