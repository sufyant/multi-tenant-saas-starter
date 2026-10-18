using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.Tenants;

// POST /v1/platform/tenants/{tenantId}/reopen: a system admin reopens a closed tenant within the reopen period, 30 days by default,
// through the system door (K4). A change of status is announced, and Audit records it under the tenant; a request that changes nothing
// announces nothing. A test of both a closure and a reopening sits here, with the use case it checks last (N9).
public sealed class ReopenTenantTests(Database database) : IAsyncLifetime
{
    private static readonly TimeSpan ReopenPeriod = TimeSpan.FromDays(30);

    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("close")]
    [InlineData("reopen")]
    public async Task CloseOrReopenTenant_UnknownTenant_IsNotFound(string action)
    {
        var response = await (await SystemAdminAsync()).PostAsync($"/v1/platform/tenants/{Guid.NewGuid()}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_found");
    }

    // K3: the platform's log names the system admin; the tenant's own log says only that the platform acted.
    [Fact]
    public async Task CloseAndReopenTenant_EachChange_IsRecordedOnceInBothLogs()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddSystemAdminAsync();
        var client = _api.CreateClient(admin, secondFactor: true);

        await _api.WaitingForMessagesAsync(() => client.CloseTenantAsync(tenant.Id));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _api.WaitingForMessagesAsync(() => client.ReopenTenantAsync(tenant.Id));

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe(
            $"tenant.closed|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd, tenant.reopened|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe(
            $"{tenant.Id}:tenant.closed|Platform|-|Tenant|{tenant.Id}|Acme Ltd, {tenant.Id}:tenant.reopened|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
    }

    // The period ends at the moment of the closure plus the period, so a second before it the tenant still comes back.
    [Fact]
    public async Task ReopenTenant_WithinThePeriod_AnswersTheTenantActive()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await SystemAdminAsync();
        await admin.CloseTenantAsync(tenant.Id);
        _time.Advance(ReopenPeriod - TimeSpan.FromSeconds(1));

        var response = await admin.ReopenTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("status").GetString(), body.GetProperty("closedAt").ValueKind).ShouldBe(("active", JsonValueKind.Null));
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Active");
    }

    [Fact]
    public async Task ReopenTenant_WhenThePeriodEnds_IsAConflictAndStaysClosed()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await SystemAdminAsync();
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));
        _time.Advance(ReopenPeriod);

        var response = await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenant.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.reopen_period_ended");
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    // Reopening goes to active and forgets the closure and the reason of a suspension before it.
    [Fact]
    public async Task ReopenTenant_ClosedAfterASuspension_ForgetsTheReason()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await SystemAdminAsync();
        await admin.SuspendTenantAsync(tenant.Id);
        var closed = await admin.CloseTenantAsync(tenant.Id);

        var reopened = await admin.ReopenTenantAsync(tenant.Id);

        (await SuspensionReasonOfAsync(closed)).ShouldBe("Unpaid invoices");
        (await SuspensionReasonOfAsync(reopened)).ShouldBeNull();
    }

    // Reopening sets a state (I1): an active tenant, as a first reopening leaves it, answers as it is and writes nothing.
    [Fact]
    public async Task ReopenTenant_Active_AnswersAsItIsAndRecordsNothing()
    {
        var tenant = await _testData.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).ReopenTenantAsync(tenant.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("active");
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    [InlineData("Suspended")]
    public async Task ReopenTenant_NeitherClosedNorActive_IsAConflictAndChangesNothing(string status)
    {
        var tenant = await _testData.AddTenantAsync(status: status);

        var response = await (await SystemAdminAsync()).ReopenTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_closed");
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    private static async Task<string?> SuspensionReasonOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("suspensionReason").GetString();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");
}
