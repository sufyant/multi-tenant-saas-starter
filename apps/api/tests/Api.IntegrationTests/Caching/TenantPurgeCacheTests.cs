using System.Net;
using System.Net.Http.Json;
using ControlPlane.Application.Tenants.StartTenantPurge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache": the tenant purge deletes the tenant's memberships, roles and permissions with bulk statements, which the change
// tracker does not see, so it removes the tenant's tag itself (BulkWriteTests). A member's next request finds no tenant at once. The
// purge schedules its timeout, so the test runs on a database of its own (section 9, rule 10).
[FixedClock("The test runs on a database of its own, so no host of another test runs what its clock schedules.")]
public sealed class TenantPurgeCacheTests(Valkey valkey, Database database) : IClassFixture<Valkey>, IAsyncLifetime
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    // The module's clock at the purge: the closure plus the reopen period, 30 days.
    private readonly FakeTimeProvider _time = new(ClosedAt + TimeSpan.FromDays(30));

    private string _databaseName = null!;

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        _api = Host("all");
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CheckAccess_AfterTheTenantIsPurged_FindsNoTenantAtOnce()
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: ClosedAt, createdAt: ClosedAt - TimeSpan.FromDays(30));
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, member);
        await _testData.SummarizeAsync(tenant.Id);
        var before = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        await _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenant.Id.ToString(), new StartTenantPurgeCommand(tenant.Id, ClosedAt), Cancellation);
            return tenant.Id;
        });
        var after = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        (before.StatusCode, await CodeOfAsync(before)).ShouldBe((HttpStatusCode.Forbidden, "tenant.closed"));
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.NotFound, "tenant.not_found"));
    }

    // The production layout (W5): the web host starts the purge, and the worker runs its later steps, which delete the tenant's rows in
    // bulk. The worker's commit removes the tenant's tag, so the web host's next request finds no tenant.
    [Fact]
    public async Task CheckAccess_AfterTheWorkerPurgesTheTenant_FindsNoTenantInTheWebHost()
    {
        await using var web = Host("web");
        await using var worker = Host("worker");
        _ = worker.Services;
        var tenant = await ClosedTenantAsync();
        var member = await _testData.AddMemberAsync(tenant);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, member);
        var before = (await web.CreateClient(member).GetAsync($"/v1/tenants/{tenant}/members", Cancellation)).StatusCode;

        using (var scope = web.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenant.ToString(), new StartTenantPurgeCommand(tenant, ClosedAt), Cancellation);
        }

        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenant}'", _databaseName) == "Purged");
        var after = await web.CreateClient(member).GetAsync($"/v1/tenants/{tenant}/members", Cancellation);

        before.ShouldBe(HttpStatusCode.Forbidden);
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.NotFound, "tenant.not_found"));
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<Guid> ClosedTenantAsync()
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: ClosedAt, createdAt: ClosedAt - TimeSpan.FromDays(30));
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.SummarizeAsync(tenant.Id);
        return tenant.Id;
    }

    private ApiFactory Host(string role) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, _databaseName),
        time: _time,
        settings: new Dictionary<string, string?> { ["Host:Role"] = role, ["ConnectionStrings:Cache"] = valkey.ConnectionString });
}
