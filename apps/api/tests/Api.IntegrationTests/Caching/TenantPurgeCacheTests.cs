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

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, databaseName);
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
            time: _time,
            settings: new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = valkey.ConnectionString });
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

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
