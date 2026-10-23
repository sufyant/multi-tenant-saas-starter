using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.IntegrationTests.Tenant.AuditLogs;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.AuditLogs;

// GET /v1/platform/audit-log: the platform's audit log, for the system admin, newest first through a cursor (K5's exception). The log is
// one table that every test shares, so each test adds records far in the future, after those of the tests before it: they are the newest,
// and the first page holds them.
public sealed class ListPlatformAuditEntriesTests(Database database) : IAsyncLifetime
{
    private readonly DateTimeOffset _start = FarFuture();

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

    [Fact]
    public async Task ListPlatformAuditEntries_AsASystemAdmin_ReturnsTheNewestEntriesFirst()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var older = await AddEntryAsync(_start);
        var newer = await AddEntryAsync(_start.AddTicks(10));

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync("/v1/platform/audit-log?limit=2", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Ids(page).ShouldBe([newer, older]);
        page.EnumerateObject().Select(field => field.Name).ShouldBe(["items", "nextCursor"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListPlatformAuditEntries_AsATenantOwner_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner, secondFactor: true).GetAsync("/v1/platform/audit-log", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListPlatformAuditEntries_TwoPages_ComeInOrder()
    {
        var client = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var oldest = await AddEntryAsync(_start);
        var middle = await AddEntryAsync(_start.AddTicks(10));
        var newest = await AddEntryAsync(_start.AddTicks(20));

        var first = await client.GetFromJsonAsync<JsonElement>("/v1/platform/audit-log?limit=2", Cancellation);
        var second = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/audit-log?limit=1&cursor={first.GetProperty("nextCursor").GetString()}", Cancellation);

        Ids(first).ShouldBe([newest, middle]);
        Ids(second).ShouldBe([oldest]);
    }

    [Fact]
    public async Task ListPlatformAuditEntries_ACursorThatDoesNotDecode_IsRejected()
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync("/v1/platform/audit-log?cursor=AQ", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"].ShouldNotBeNull().ToString().ShouldBe("paging.cursor_invalid");
    }

    // K3: in the platform's log a system admin is a user, by name. A deleted user has no name and is marked deleted; a tenant keeps its
    // name of that moment.
    [Fact]
    public async Task ListPlatformAuditEntries_ActorsAndTargets_AreNamedOnRead()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var grace = await _testData.UserIdOfAsync(await _testData.AddUserAsync(firstName: "Grace", lastName: "Hopper"));
        var deletedUser = await _testData.AddUserAsync(firstName: "Gone", lastName: "Away");
        await _testData.DeleteUserAsync(deletedUser);
        var deleted = await _testData.UserIdOfAsync(deletedUser);
        var tenant = Guid.CreateVersion7();
        await _audit.AddPlatformEntryAsync(_start, "User", grace, "User", deleted, "user.deleted");
        await _audit.AddPlatformEntryAsync(_start.AddTicks(10), "User", grace, "Tenant", tenant, "tenant.suspended", "Globex");

        var page = await _api.CreateClient(admin, secondFactor: true).GetFromJsonAsync<JsonElement>("/v1/platform/audit-log?limit=2", Cancellation);

        ListAuditEntriesTests.Described(page).ShouldBe(
        [
            $"tenant.suspended|user|{grace}|Grace Hopper|false|tenant|{tenant}|Globex|-",
            $"user.deleted|user|{grace}|Grace Hopper|false|user|{deleted}|-|true",
        ]);
    }

    [Fact]
    public async Task ListPlatformAuditEntries_AUserWithAnEmailAddress_AnswersNoEmailAddress()
    {
        var marker = Guid.NewGuid().ToString("N");
        var admin = await _testData.AddSystemAdminAsync(email: $"admin-{marker}@example.com");
        var target = await _testData.UserIdOfAsync(await _testData.AddUserAsync(email: $"target-{marker}@example.com", firstName: "Ada"));
        await _audit.AddPlatformEntryAsync(_start, "User", await _testData.UserIdOfAsync(admin), "User", target);

        var answer = await _api.CreateClient(admin, secondFactor: true).GetStringAsync("/v1/platform/audit-log?limit=1", Cancellation);

        answer.ShouldNotContain(marker);
        ListAuditEntriesTests.Described(JsonElement.Parse(answer)).ShouldBe([$"user.suspended|user|{await _testData.UserIdOfAsync(admin)}|-|false|user|{target}|Ada|false"]);
    }

    // The Activity tab of a tenant: the platform's entries about that tenant, newest first, a page at a time. An entry about
    // another tenant or about a user is left out. The platform never reads the tenant's own log here.
    [Fact]
    public async Task ListPlatformAuditEntries_ATenantFilter_ReturnsTheEntriesAboutThatTenantOnly()
    {
        var client = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var (tenant, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var created = await _audit.AddPlatformEntryAsync(_start, "User", Guid.CreateVersion7(), "Tenant", tenant, "tenant.created", "Acme Ltd");
        await _audit.AddPlatformEntryAsync(_start.AddTicks(10), "User", Guid.CreateVersion7(), "Tenant", other, "tenant.created", "Globex");
        await _audit.AddPlatformEntryAsync(_start.AddTicks(20), "User", Guid.CreateVersion7(), "User", tenant);
        var suspended = await _audit.AddPlatformEntryAsync(_start.AddTicks(30), "User", Guid.CreateVersion7(), "Tenant", tenant, "tenant.suspended", "Acme Ltd");
        await _audit.AddTenantEntryAsync(tenant, _start.AddTicks(40), targetKind: "Tenant", targetId: tenant);

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/audit-log?tenantId={tenant}&limit=1", Cancellation);
        var second = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/platform/audit-log?tenantId={tenant}&limit=1&cursor={first.GetProperty("nextCursor").GetString()}", Cancellation);

        Ids(first).ShouldBe([suspended]);
        Ids(second).ShouldBe([created]);
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // K3: the reason of a suspension is in the platform's log only. The stored shape, as RecordTenantSuspendedHandler writes it, answers
    // the shape of tenant.suspended.
    [Fact]
    public async Task ListPlatformAuditEntries_AStoredReasonOfASuspension_AnswersTheShapeOfTenantSuspended()
    {
        var client = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = Guid.CreateVersion7();
        await _audit.AddPlatformEntryAsync(_start, "User", Guid.CreateVersion7(), "Tenant", tenant, "tenant.suspended", "Acme Ltd", """{"reason":"Unpaid invoices"}""");

        var page = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/audit-log?tenantId={tenant}", Cancellation);

        page.GetProperty("items")[0].GetProperty("details").GetRawText().ShouldBe("""{"operation":"tenant.suspended","reason":"Unpaid invoices"}""");
    }

    // Owner recovery (section 6): the platform's log keeps tenant.owner_made, as RecordOwnerMadeHandler writes it, and answers its shape:
    // the member's id and the role they had.
    [Fact]
    public async Task ListPlatformAuditEntries_AStoredOwnerMade_AnswersTheShapeOfTenantOwnerMade()
    {
        var client = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = Guid.CreateVersion7();
        await _audit.AddPlatformEntryAsync(
            _start,
            "User",
            Guid.CreateVersion7(),
            "Tenant",
            tenant,
            "tenant.owner_made",
            "Acme Ltd",
            """{"userId":"0199f7a0-0000-7000-8000-000000000001","oldRole":"member","oldRoleId":"0199f7a0-0000-7000-8000-000000000004","oldRoleBuiltIn":true}""");

        var page = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/audit-log?tenantId={tenant}", Cancellation);

        page.GetProperty("items")[0].GetProperty("details").GetRawText().ShouldBe(
            """{"operation":"tenant.owner_made","userId":"0199f7a0-0000-7000-8000-000000000001","oldRole":{"id":"0199f7a0-0000-7000-8000-000000000004","name":"member","builtIn":true}}""");
    }

    [Fact]
    public async Task ListPlatformAuditEntries_ATenantFilterThatIsNoId_IsABadRequest()
    {
        var client = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var response = await client.GetAsync("/v1/platform/audit-log?tenantId=acme", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"].ShouldNotBeNull().ToString().ShouldBe("request.invalid");
    }

    // 500 years from now, in whole microseconds, as PostgreSQL keeps a moment. A later test starts later.
    private static DateTimeOffset FarFuture()
    {
        var moment = DateTimeOffset.UtcNow.AddYears(500);
        return moment.AddTicks(-(moment.Ticks % 10));
    }

    private static IEnumerable<Guid> Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

    private Task<Guid> AddEntryAsync(DateTimeOffset occurredAt) =>
        _audit.AddPlatformEntryAsync(occurredAt, "User", Guid.CreateVersion7(), "User", Guid.CreateVersion7());
}
