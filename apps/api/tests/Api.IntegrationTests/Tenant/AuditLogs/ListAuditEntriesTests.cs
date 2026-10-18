using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant.AuditLogs;

// GET /v1/tenants/{tenantId}/audit-log: the tenant's audit log, "Activity" on the screen, for its Owners and Admins. K5's exception: a
// cursor, newest first by the moment and then the id, and no total. Names are resolved on read (K3); the answer holds no email address.
// Each test adds the records of a tenant of its own.
public sealed class ListAuditEntriesTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Catalog _catalog = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListAuditEntries_AsAnOwner_ReturnsTheTenantsEntriesNewestFirst()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var older = await _audit.AddTenantEntryAsync(tenant.Id, Start);
        var newer = await _audit.AddTenantEntryAsync(tenant.Id, Start.AddMinutes(1));

        var response = await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Ids(page).ShouldBe([newer, older]);
        page.EnumerateObject().Select(field => field.Name).ShouldBe(["items", "nextCursor"], ignoreOrder: true);
        page.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "occurredAt", "operation", "details", "actor", "target"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListAuditEntries_AsAnAdmin_IsAllowed()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddMemberAsync(tenant.Id, role: "Admin");

        var response = await _api.CreateClient(admin).GetAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListAuditEntries_AsAMember_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Row level security keeps the log to the tenant of the path; the platform's log is not read through a tenant route (R11), not even
    // its record about this tenant.
    [Fact]
    public async Task ListAuditEntries_RecordsOfAnotherTenantAndOfThePlatform_NeverAppear()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var own = await _audit.AddTenantEntryAsync(tenant.Id, Start);
        await _audit.AddTenantEntryAsync(other.Id, Start.AddMinutes(1));
        await _audit.AddPlatformEntryAsync(Start.AddMinutes(2), "User", Guid.CreateVersion7(), "Tenant", tenant.Id, "tenant.suspended", "Acme Ltd");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        Ids(page).ShouldBe([own]);
    }

    [Fact]
    public async Task ListAuditEntries_ThreePages_ComeInOrderAndTheLastHasNoNextCursor()
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var entries = await AddEntriesAsync(tenant.Id, 5);

        var first = await PageAsync(client, tenant.Id, "limit=2");
        var second = await PageAsync(client, tenant.Id, $"limit=2&cursor={first.GetProperty("nextCursor").GetString()}");
        var third = await PageAsync(client, tenant.Id, $"limit=2&cursor={second.GetProperty("nextCursor").GetString()}");

        Ids(first).ShouldBe([entries[4], entries[3]]);
        Ids(second).ShouldBe([entries[2], entries[1]]);
        Ids(third).ShouldBe([entries[0]]);
        third.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The cursor holds the last item's moment and id, so an entry written after the first page does not push an item onto the next.
    [Fact]
    public async Task ListAuditEntries_AnEntryWrittenBetweenTwoRequests_DoesNotShiftThePage()
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var entries = await AddEntriesAsync(tenant.Id, 4);
        var first = await PageAsync(client, tenant.Id, "limit=2");
        await _audit.AddTenantEntryAsync(tenant.Id, Start.AddHours(1));

        var second = await PageAsync(client, tenant.Id, $"limit=2&cursor={first.GetProperty("nextCursor").GetString()}");

        Ids(second).ShouldBe([entries[1], entries[0]]);
    }

    // Two entries at the same moment are ordered by their ids, so neither is lost or repeated at a page's edge.
    [Fact]
    public async Task ListAuditEntries_TwoEntriesAtTheSameMomentAcrossAPageEdge_AppearOnceEach()
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var one = await _audit.AddTenantEntryAsync(tenant.Id, Start);
        var another = await _audit.AddTenantEntryAsync(tenant.Id, Start);

        var first = await PageAsync(client, tenant.Id, "limit=1");
        var second = await PageAsync(client, tenant.Id, $"limit=1&cursor={first.GetProperty("nextCursor").GetString()}");

        Ids(first).Concat(Ids(second)).ShouldBe([one, another], ignoreOrder: true);
    }

    [Theory]
    [InlineData("cursor=not-a-cursor", "paging.cursor_invalid")]
    [InlineData("limit=0", "paging.limit_invalid")]
    [InlineData("limit=101", "paging.limit_invalid")]
    public async Task ListAuditEntries_AnInvalidCursorOrLimit_IsRejected(string query, string code)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant.Id}/audit-log?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"].ShouldNotBeNull().ToString().ShouldBe(code);
    }

    [Fact]
    public async Task ListAuditEntries_ACursorThatWasChanged_IsRejected()
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        await AddEntriesAsync(tenant.Id, 2);
        var changed = Changed((await PageAsync(client, tenant.Id, "limit=1")).GetProperty("nextCursor").GetString()!);

        var response = await client.GetAsync($"/v1/tenants/{tenant.Id}/audit-log?cursor={changed}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"].ShouldNotBeNull().ToString().ShouldBe("paging.cursor_invalid");
    }

    // K3: the actor and a target person are ids; their names come from the catalog when the log is read. A name is only a person's real
    // name: the platform, which stands for a system admin in a tenant's log, has none, and the screen names it from its kind. A deleted
    // user has no name and is marked deleted; an id the catalog does not know has no name. A tenant keeps its name of that moment; an
    // invitation has none.
    [Fact]
    public async Task ListAuditEntries_ActorsAndTargets_AreNamedOnRead()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var ada = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync(firstName: "Ada", lastName: "Lovelace"));
        var deletedUser = await _catalog.AddUserAsync(firstName: "Gone", lastName: "Away");
        await _catalog.DeleteUserAsync(deletedUser);
        var deleted = await _catalog.UserIdOfAsync(deletedUser);
        var unknown = Guid.CreateVersion7();
        var invitation = Guid.CreateVersion7();
        await _audit.AddTenantEntryAsync(tenant.Id, Start, "User", ada, "member.removed", "User", deleted);
        await _audit.AddTenantEntryAsync(tenant.Id, Start.AddMinutes(1), "Platform", null, "tenant.suspended", "Tenant", tenant.Id, "Acme Then");
        await _audit.AddTenantEntryAsync(tenant.Id, Start.AddMinutes(2), "User", unknown, "invitation.revoked", "Invitation", invitation);

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        Described(page).ShouldBe(
        [
            $"invitation.revoked|user|{unknown}|-|false|invitation|{invitation}|-|-",
            $"tenant.suspended|platform|-|-|-|tenant|{tenant.Id}|Acme Then|-",
            $"member.removed|user|{ada}|Ada Lovelace|false|user|{deleted}|-|true",
        ]);
    }

    [Fact]
    public async Task ListAuditEntries_DetailsOfARecord_AreItsStoredObject()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _audit.AddTenantEntryAsync(tenant.Id, Start, details: """{"oldRole":"member","newRole":"admin"}""");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        var details = page.GetProperty("items")[0].GetProperty("details");
        (details.GetProperty("oldRole").GetString(), details.GetProperty("newRole").GetString()).ShouldBe(("member", "admin"));
    }

    // The screen shows names; an email address is a person's data that Activity does not need, even for a user without a name, who gets
    // no name at all.
    [Fact]
    public async Task ListAuditEntries_UsersWithAndWithoutAName_AnswerNoEmailAddress()
    {
        var marker = Guid.NewGuid().ToString("N");
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var named = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync(email: $"named-{marker}@example.com", firstName: "Ada", lastName: "Lovelace"));
        var unnamed = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync(email: $"unnamed-{marker}@example.com"));
        await _audit.AddTenantEntryAsync(tenant.Id, Start, "User", named, "member.role_changed", "User", unnamed);

        var answer = await _api.CreateClient(owner).GetStringAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        answer.ShouldNotContain(marker);
        Described(JsonElement.Parse(answer)).ShouldBe([$"member.role_changed|user|{named}|Ada Lovelace|false|user|{unnamed}|-|false"]);
    }

    // The tenant's records, one minute apart, oldest first.
    private async Task<Guid[]> AddEntriesAsync(Guid tenantId, int count)
    {
        var entries = new Guid[count];
        foreach (var minute in Enumerable.Range(0, count))
        {
            entries[minute] = await _audit.AddTenantEntryAsync(tenantId, Start.AddMinutes(minute));
        }

        return entries;
    }

    // The cursor with one character in its id changed: it still decodes, but its checksum no longer matches.
    private static string Changed(string cursor) => string.Concat(cursor[..20], cursor[20] == 'A' ? "B" : "A", cursor[21..]);

    private static async Task<JsonElement> PageAsync(HttpClient client, Guid tenantId, string query) =>
        await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenantId}/audit-log?{query}", Cancellation);

    private static IEnumerable<Guid> Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

    // Each entry as "operation|actor kind|actor user id|actor name|actor deleted|target kind|target id|target name|target deleted", with
    // "-" for an empty value.
    internal static IEnumerable<string> Described(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item =>
    {
        var actor = item.GetProperty("actor");
        var target = item.GetProperty("target");
        return string.Join('|', item.GetProperty("operation").GetString(), actor.GetProperty("kind").GetString(), TextOf(actor.GetProperty("userId")),
            TextOf(actor.GetProperty("displayName")), TextOf(actor.GetProperty("deleted")), target.GetProperty("kind").GetString(),
            target.GetProperty("id").GetString(), TextOf(target.GetProperty("name")), TextOf(target.GetProperty("deleted")));
    });

    private static string TextOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "-",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetString()!,
    };
}
