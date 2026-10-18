using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Tenants;

// POST /v1/platform/tenants/{tenantId}/suspend: a system admin suspends a tenant, for a reason they write, through the system door. A
// change of status is announced, and Audit records it under the tenant; a request that changes nothing announces nothing.
// SuspendedTenantTests follows what a suspended tenant means to its members.
public sealed class SuspendTenantTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private const string Reason = "Unpaid invoices";

    private readonly Catalog _catalog = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

    [Fact]
    public async Task SuspendTenant_Twice_IsRecordedOnceInEachLog()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await SystemAdminAsync();
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id));

        var again = await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id, "Another reason"));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SuspensionReasonOfAsync(again)).ShouldBe(Reason);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    // The reason is kept without the spaces around it, answered to the system admin, and read again with the tenant.
    [Fact]
    public async Task SuspendTenant_WithAReason_KeepsItTrimmedAndShowsItToTheAdmin()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await SystemAdminAsync();

        var response = await admin.SuspendTenantAsync(tenant.Id, "  Unpaid invoices ");

        (await SuspensionReasonOfAsync(response)).ShouldBe(Reason);
        (await SuspensionReasonOfAsync(await admin.GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation))).ShouldBe(Reason);
    }

    [Fact]
    public async Task SuspendTenant_AReasonOfFiveHundredCharactersWithSpacesAround_IsAccepted()
    {
        var tenant = await _catalog.AddTenantAsync();
        var reason = new string('r', 500);

        var response = await (await SystemAdminAsync()).SuspendTenantAsync(tenant.Id, $" {reason} ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SuspensionReasonOfAsync(response)).ShouldBe(reason);
    }

    [Theory]
    [MemberData(nameof(WrongReasons))]
    public async Task SuspendTenant_WithoutAReasonOrWithOneTooLong_IsABadRequestAndChangesNothing(string body, string fieldCode)
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await (await SystemAdminAsync()).PostAsync(
            $"/v1/platform/tenants/{tenant.Id}/suspend", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("reason").EnumerateArray().Select(code => code.GetString()).ShouldBe([fieldCode]);
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe("Active");
    }

    // K3: the platform's entry holds the system admin's reason; the tenant's entry, which its Owners and Admins read, does not.
    [Fact]
    public async Task SuspendTenant_WithAReason_RecordsItInThePlatformEntryOnly()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await SystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id));

        (await _audit.PlatformDetailsAsync(tenant.Id)).ShouldBe($$"""{"reason": "{{Reason}}"}""");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe("-");
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, Reason)).ShouldBe(0);
    }

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    public static TheoryData<string, string> WrongReasons => new()
    {
        { """{}""", "required" },
        { """{"reason":null}""", "required" },
        { """{"reason":""}""", "required" },
        { """{"reason":"   "}""", "required" },
        { $$"""{"reason":"{{new string('r', 501)}}"}""", "too_long" },
    };

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");

    private static async Task<string?> SuspensionReasonOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("suspensionReason").GetString();
}
