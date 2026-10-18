using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// A system admin suspends a tenant, for a reason they write, and reactivates it, through the system door. A change of status is
// announced, and Audit entries it under the tenant; a request that changes nothing announces nothing. SuspendedTenantTests follows
// what a suspended tenant means to its members.
public sealed class TenantSuspensionTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private const string Reason = "Unpaid invoices";

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

        var response = await (await AdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{tenant.Id}/{action}", new { reason = Reason }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "suspensionReason", "closedAt"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(tenant.Id);
        body.GetProperty("slug").GetString().ShouldBe(tenant.Slug);
        body.GetProperty("status").GetString().ShouldBe(answered);
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(stored);
    }

    // A tenant whose creation has not passed its pivot, or never will, is neither suspended nor reactivated, and a closed one comes back
    // only through a reopening.
    [Theory]
    [InlineData("Provisioning", "suspend")]
    [InlineData("Provisioning", "reactivate")]
    [InlineData("Cancelled", "suspend")]
    [InlineData("Cancelled", "reactivate")]
    [InlineData("Closed", "suspend")]
    [InlineData("Closed", "reactivate")]
    public async Task SuspendOrReactivateTenant_ProvisioningCancelledOrClosed_IsAConflictAndChangesNothing(string status, string action)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{tenant.Id}/{action}", new { reason = Reason }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_active");
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task SuspendOrReactivateTenant_UnknownTenant_IsNotFound(string action)
    {
        var response = await (await AdminAsync()).PostAsJsonAsync($"/v1/platform/tenants/{Guid.NewGuid()}/{action}", new { reason = Reason }, Cancellation);

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

        await _api.WaitingForMessagesAsync(() => client.SuspendTenantAsync(tenant.Id));
        await _api.WaitingForMessagesAsync(() => client.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

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
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id));

        var again = await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id, "Another reason"));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SuspensionReasonOfAsync(again)).ShouldBe(Reason);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task ReactivateTenant_AlreadyActive_IsNotRecorded()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await AdminAsync()).PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(0);
    }

    // The reason is kept without the spaces around it, answered to the system admin, and read again with the tenant.
    [Fact]
    public async Task SuspendTenant_WithAReason_KeepsItTrimmedAndShowsItToTheAdmin()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();

        var response = await admin.SuspendTenantAsync(tenant.Id, "  Unpaid invoices ");

        (await SuspensionReasonOfAsync(response)).ShouldBe(Reason);
        (await SuspensionReasonOfAsync(await admin.GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation))).ShouldBe(Reason);
    }

    [Fact]
    public async Task SuspendTenant_AReasonOfFiveHundredCharactersWithSpacesAround_IsAccepted()
    {
        var tenant = await _catalog.AddTenantAsync();
        var reason = new string('r', 500);

        var response = await (await AdminAsync()).SuspendTenantAsync(tenant.Id, $" {reason} ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SuspensionReasonOfAsync(response)).ShouldBe(reason);
    }

    public static TheoryData<string, string> WrongReasons => new()
    {
        { """{}""", "required" },
        { """{"reason":null}""", "required" },
        { """{"reason":""}""", "required" },
        { """{"reason":"   "}""", "required" },
        { $$"""{"reason":"{{new string('r', 501)}}"}""", "too_long" },
    };

    [Theory]
    [MemberData(nameof(WrongReasons))]
    public async Task SuspendTenant_WithoutAReasonOrWithOneTooLong_IsABadRequestAndChangesNothing(string body, string fieldCode)
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await (await AdminAsync()).PostAsync(
            $"/v1/platform/tenants/{tenant.Id}/suspend", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("reason").EnumerateArray().Select(code => code.GetString()).ShouldBe([fieldCode]);
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe("Active");
    }

    [Fact]
    public async Task ReactivateTenant_Suspended_ForgetsTheReason()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        await admin.SuspendTenantAsync(tenant.Id);

        var response = await admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation);

        (await SuspensionReasonOfAsync(response)).ShouldBeNull();
        (await SuspensionReasonOfAsync(await admin.GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation))).ShouldBeNull();
    }

    // K3: the platform's record holds the admin's reason; the tenant's record, which its Owners and Admins read, does not.
    [Fact]
    public async Task SuspendTenant_WithAReason_RecordsItInThePlatformEntryOnly()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();

        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id));

        (await _audit.PlatformDetailsAsync(tenant.Id)).ShouldBe($$"""{"reason": "{{Reason}}"}""");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe("-");
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, Reason)).ShouldBe(0);
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");

    private static async Task<string?> SuspensionReasonOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("suspensionReason").GetString();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
