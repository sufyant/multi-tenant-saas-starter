using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform.Tenants;

// GET /v1/platform/tenants: every tenant, in every status, for the system admin. The list reads across tenants: a test that reads the
// whole list has a database of its own, where no other test adds a tenant; any other test searches for a marker of its own.
public sealed class ListTenantsTests(Database database) : IAsyncLifetime
{
    private readonly string _marker = Guid.NewGuid().ToString("N")[..12];

    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListTenants_TenantsInEveryStatus_ReturnsEachByNameThenId()
    {
        await UseADatabaseOfItsOwnAsync();
        var random = Guid.NewGuid().ToString();
        var delta = await _testData.AddTenantAsync(name: "Delta Ltd");
        var alpha = await _testData.AddTenantAsync(status: "Provisioning", name: "Alpha Ltd");
        var charlie = await _testData.AddTenantAsync(status: "Suspended", name: "Charlie Ltd");
        var bravo = await _testData.AddTenantAsync(status: "Cancelled", name: "Bravo Ltd");
        var echoLater = await _testData.AddTenantAsync(name: "Echo Ltd", tenantId: Guid.Parse($"f{random[1..]}"));
        var echoEarlier = await _testData.AddTenantAsync(name: "Echo Ltd", tenantId: Guid.Parse($"0{random[1..]}"));

        var response = await (await SystemAdminAsync()).GetAsync("/v1/platform/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Tenants(page).ShouldBe(
        [
            (alpha.Id, "Alpha Ltd", alpha.Slug, "provisioning"),
            (bravo.Id, "Bravo Ltd", bravo.Slug, "cancelled"),
            (charlie.Id, "Charlie Ltd", charlie.Slug, "suspended"),
            (delta.Id, "Delta Ltd", delta.Slug, "active"),
            (echoEarlier.Id, "Echo Ltd", echoEarlier.Slug, "active"),
            (echoLater.Id, "Echo Ltd", echoLater.Slug, "active"),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name).ShouldBe(
            ["id", "name", "slug", "status", "suspensionReason", "closedAt", "reopenableUntil", "memberCount", "ownerCount", "ownerInvitation"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(6);
    }

    [Fact]
    public async Task ListTenants_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        await UseADatabaseOfItsOwnAsync();
        await _testData.AddTenantAsync(name: "First Ltd");
        await _testData.AddTenantAsync(name: "Second Ltd");
        var third = await _testData.AddTenantAsync(name: "Third Ltd");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>("/v1/platform/tenants?page=2&pageSize=2", Cancellation);

        Tenants(page).ShouldBe([(third.Id, "Third Ltd", third.Slug, "active")]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101", "paging.page_size_invalid")]
    [InlineData("page=0", "paging.page_invalid")]
    public async Task ListTenants_PagingOutsideItsRange_IsRejected(string query, string code)
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    [Theory]
    [InlineData("acme", "Alpha")]
    [InlineData("ACME", "Alpha")]
    [InlineData("orbit-", "Bravo")]
    [InlineData("ORBIT-", "Bravo")]
    public async Task ListTenants_ASearch_FindsTheTenantsWhoseNameOrSlugContainsItInAnyCase(string q, string found)
    {
        await _testData.AddTenantAsync(name: $"Alpha Acme{_marker} Ltd", slug: $"alpha-{_marker}");
        await _testData.AddTenantAsync(name: $"Bravo {_marker} Ltd", slug: $"orbit-{_marker}");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={Uri.EscapeDataString(q + _marker)}", Cancellation);

        Names(page).ShouldHaveSingleItem().ShouldStartWith(found);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("100%", "100% Ltd")]
    [InlineData("a_b", "a_b Ltd")]
    [InlineData(@"c\d", @"c\d Ltd")]
    public async Task ListTenants_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        await AddTenantsAsync("100% Ltd", "1000 Ltd", "a_b Ltd", "axb Ltd", @"c\d Ltd", "cd Ltd");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={Uri.EscapeDataString($"{_marker} {q}")}", Cancellation);

        Names(page).ShouldBe([$"{_marker} {found}"]);
    }

    [Theory]
    [InlineData("provisioning", "Alpha")]
    [InlineData("active", "Bravo")]
    [InlineData("suspended", "Charlie")]
    [InlineData("cancelled", "Delta")]
    [InlineData("closed", "Echo")]
    public async Task ListTenants_AStatusFilter_ReturnsTheTenantsInThatStatusOnly(string status, string found)
    {
        await _testData.AddTenantAsync(status: "Provisioning", name: $"{_marker} Alpha");
        await _testData.AddTenantAsync(status: "Active", name: $"{_marker} Bravo");
        await _testData.AddTenantAsync(status: "Suspended", name: $"{_marker} Charlie");
        await _testData.AddTenantAsync(status: "Cancelled", name: $"{_marker} Delta");
        await _testData.AddTenantAsync(status: "Closed", name: $"{_marker} Echo", closedAt: DateTimeOffset.UtcNow);

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}&status={status}", Cancellation);

        Names(page).ShouldBe([$"{_marker} {found}"]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("name", "Alpha, Bravo, Charlie")]
    [InlineData("-name", "Charlie, Bravo, Alpha")]
    [InlineData("slug", "Charlie, Bravo, Alpha")]
    [InlineData("-slug", "Alpha, Bravo, Charlie")]
    public async Task ListTenants_ASortField_OrdersByItInItsDirection(string sort, string names)
    {
        await _testData.AddTenantAsync(name: $"{_marker} Bravo", slug: $"yankee-{_marker}");
        await _testData.AddTenantAsync(name: $"{_marker} Alpha", slug: $"zulu-{_marker}");
        await _testData.AddTenantAsync(name: $"{_marker} Charlie", slug: $"xray-{_marker}");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}&sort={sort}", Cancellation);

        string.Join(", ", Names(page).Select(name => name[(_marker.Length + 1)..])).ShouldBe(names);
    }

    // The query string sends a value left empty; like an empty search or sort, it is no filter.
    [Fact]
    public async Task ListTenants_AnEmptyStatus_IsNoFilter()
    {
        await _testData.AddTenantAsync(status: "Active", name: $"{_marker} Bravo");
        await _testData.AddTenantAsync(status: "Suspended", name: $"{_marker} Charlie");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}&status=", Cancellation);

        Names(page).ShouldBe([$"{_marker} Bravo", $"{_marker} Charlie"]);
    }

    // Every sort ends with the id, so two tenants of the same name keep their places from one page to the next.
    [Theory]
    [InlineData("name")]
    [InlineData("-name")]
    public async Task ListTenants_TwoTenantsWithTheSameSortValue_KeepTheirOrderAcrossPages(string sort)
    {
        var random = Guid.NewGuid().ToString();
        var later = await _testData.AddTenantAsync(name: $"{_marker} Same", tenantId: Guid.Parse($"f{random[1..]}"));
        var earlier = await _testData.AddTenantAsync(name: $"{_marker} Same", tenantId: Guid.Parse($"0{random[1..]}"));
        var admin = await SystemAdminAsync();

        var first = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}&sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}&sort={sort}&pageSize=1&page=2", Cancellation);

        Tenants(first).Concat(Tenants(second)).Select(tenant => tenant.Id).ShouldBe([earlier.Id, later.Id]);
    }

    [Theory]
    [InlineData("sort=age", "paging.sort_invalid")]
    [InlineData("sort=Name", "paging.sort_invalid")]
    [InlineData("sort=name,slug", "paging.sort_invalid")]
    [InlineData("status=Active", "paging.filter_invalid")]
    [InlineData("status=deleted", "paging.filter_invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListTenants_AnUnknownSortOrFilterValueOrALongSearch_IsRejected(string query, string code)
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    // The system admin sees why each tenant is suspended in the list too.
    [Fact]
    public async Task ListTenants_ASuspendedTenant_ShowsItsReason()
    {
        await _testData.AddTenantAsync(status: "Suspended", name: $"{_marker} Alpha", suspensionReason: "Unpaid invoices");
        await _testData.AddTenantAsync(name: $"{_marker} Bravo");

        var page = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}", Cancellation);

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("suspensionReason").GetString()).ShouldBe(["Unpaid invoices", null]);
    }

    // Each item counts the tenant's members and Owners, and, while the tenant has no Owner, names its latest
    // Owner invitation, whose state "expired" comes from its expiry at the moment of the request. A closed tenant says until when it can
    // be reopened. The tenants are made through the API, so their summary comes from the commands (R12).
    [Fact]
    public async Task ListTenants_TenantsInEachStateOfTheirOwners_AnswerTheCountsAndTheInvitation()
    {
        var admin = await SystemAdminAsync();
        var tenants = new PlatformTenants(database, _api);
        var (ada, grace, expired, pending, closed) = (Email(), Email(), Email(), Email(), Email());
        var expiredTenant = await tenants.CreateAsync(admin, expired, name: $"{_marker} Delta");
        var expiredAt = _time.GetUtcNow().AddDays(7);
        _time.Advance(TimeSpan.FromDays(8));
        var owned = await tenants.CreateAsync(admin, ada, name: $"{_marker} Alpha");
        var (first, _) = await tenants.AcceptAsync(await tenants.LatestInvitationAsync(owned), ada);
        var graces = await PlatformTenants.InvitationIdOfAsync(
            await _api.WaitingForMessagesAsync(() => _api.CreateClient(first).InviteMemberAsync(owned, grace, role: "owner")));
        _time.Advance(TimeSpan.FromMinutes(1));
        await tenants.AcceptAsync(graces, grace, "Grace", "Hopper");
        var pendingTenant = await tenants.CreateAsync(admin, pending, name: $"{_marker} Bravo");
        var closedTenant = await tenants.CreateAsync(admin, closed, name: $"{_marker} Charlie");
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(closedTenant));
        var now = _time.GetUtcNow();

        var page = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants?q={_marker}", Cancellation);

        Owners(page).ShouldBe(
        [
            $"{owned}|2|2|-|-",
            $"{pendingTenant}|0|0|{pending}|{now.AddDays(7):O}|pending|-",
            $"{closedTenant}|0|0|{closed}|{now.AddDays(7):O}|cancelled|{now.AddDays(30):O}",
            $"{expiredTenant}|0|0|{expired}|{expiredAt:O}|expired|-",
        ]);
    }

    // Deployment, section 8: the version before this one writes the summary without the language while the migration has already run.
    // The page then answers that invitation without a language and the other one with its own, never an error (section 13).
    [Fact]
    public async Task ListTenants_ASummaryWithoutTheLanguage_AnswersThePageWithoutIt()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(language: "tr"), secondFactor: true);
        var tenants = new PlatformTenants(database, _api);
        var without = await tenants.CreateAsync(admin, Email(), name: $"{_marker} Alpha");
        await tenants.CreateAsync(admin, Email(), name: $"{_marker} Bravo");
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.tenants SET owner_invitation_language = NULL WHERE id = '{without}'");

        var response = await admin.GetAsync($"/v1/platform/tenants?q={_marker}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("ownerInvitation").GetProperty("language").ToString())
            .ShouldBe(["", "tr"]);
    }

    private static List<(Guid Id, string Name, string Slug, string Status)> Tenants(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("name").GetString()!,
            item.GetProperty("slug").GetString()!,
            item.GetProperty("status").GetString()!)),
    ];

    // Each item as "id|memberCount|ownerCount|ownerInvitation|reopenableUntil": the invitation as "email|expiresAt|state", "-" for null.
    private static List<string> Owners(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => string.Join('|',
            item.GetProperty("id").GetGuid(),
            item.GetProperty("memberCount").GetInt32(),
            item.GetProperty("ownerCount").GetInt32(),
            item.GetProperty("ownerInvitation") is { ValueKind: JsonValueKind.Object } invitation
                ? $"{invitation.GetProperty("email").GetString()}|{invitation.GetProperty("expiresAt").GetDateTimeOffset():O}|{invitation.GetProperty("state").GetString()}"
                : "-",
            item.GetProperty("reopenableUntil") is { ValueKind: JsonValueKind.String } until ? until.GetDateTimeOffset().ToString("O") : "-")),
    ];

    private static string Email() => $"{Guid.NewGuid():N}@example.com";

    private static List<string> Names(JsonElement page) => [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()!)];

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // A database where no other test adds a tenant, for a test that reads the whole list.
    private async Task UseADatabaseOfItsOwnAsync()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, name);
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name));
    }

    // Tenants named after the test's marker, so that a search for the marker finds them alone.
    private async Task AddTenantsAsync(params string[] names)
    {
        foreach (var name in names)
        {
            await _testData.AddTenantAsync(name: $"{_marker} {name}");
        }
    }
}
