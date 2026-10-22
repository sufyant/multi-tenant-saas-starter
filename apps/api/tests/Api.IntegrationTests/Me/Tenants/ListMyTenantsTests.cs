using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Me.Tenants;

// GET /v1/me/tenants runs without a tenant (T4): the caller's active and suspended tenants, read through the policy on their own
// memberships.
public sealed class ListMyTenantsTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListMyTenants_MemberOfTwoTenants_ReturnsThoseTwoByName()
    {
        var beta = await _testData.AddTenantAsync(name: "Beta Ltd");
        var alpha = await _testData.AddTenantAsync(name: "Alpha Ltd");
        var user = await _testData.AddMemberAsync(beta.Id);
        await _testData.AddMemberAsync(alpha.Id, user, role: "Owner");
        var notMine = await _testData.AddTenantAsync(name: "Aardvark Ltd");
        await _testData.AddMemberAsync(notMine.Id);

        var response = await _api.CreateClient(user).GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Tenants(page).ShouldBe([(alpha.Id, "Alpha Ltd", alpha.Slug), (beta.Id, "Beta Ltd", beta.Slug)]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "role"], ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(2);
    }

    // The chooser shows the caller's role in each tenant, in lower case as every role the API answers (K5).
    [Fact]
    public async Task ListMyTenants_MemberOfThreeTenantsInThreeRoles_ReturnsTheRoleInEach()
    {
        var owned = await _testData.AddTenantAsync(name: "Alpha Ltd");
        var administered = await _testData.AddTenantAsync(name: "Bravo Ltd");
        var joined = await _testData.AddTenantAsync(name: "Charlie Ltd");
        var user = await _testData.AddMemberAsync(owned.Id, role: "Owner");
        await _testData.AddMemberAsync(administered.Id, user, role: "Admin");
        await _testData.AddMemberAsync(joined.Id, user, role: "Member");

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), Roles.Of(item)))
            .ShouldBe([(owned.Id, Roles.BuiltIn("owner")), (administered.Id, Roles.BuiltIn("admin")), (joined.Id, Roles.BuiltIn("member"))]);
    }

    // The page is one query, with the role joined: a longer page sends no more commands (K5).
    [Fact]
    public async Task ListMyTenants_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var user = await MemberOfAsync(
            await _testData.AddTenantAsync(name: "Alpha Ltd"),
            await _testData.AddTenantAsync(name: "Bravo Ltd"),
            await _testData.AddTenantAsync(name: "Charlie Ltd"));

        var one = await commands.CountAsync(() => _api.CreateClient(user), client => client.GetAsync("/v1/me/tenants?pageSize=1", Cancellation));
        var three = await commands.CountAsync(() => _api.CreateClient(user), client => client.GetAsync("/v1/me/tenants?pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListMyTenants_TwoTenantsOfTheSameName_AreOrderedById()
    {
        var random = Guid.NewGuid().ToString();
        var later = await _testData.AddTenantAsync(name: "Same Ltd", tenantId: Guid.Parse($"f{random[1..]}"));
        var earlier = await _testData.AddTenantAsync(name: "Same Ltd", tenantId: Guid.Parse($"0{random[1..]}"));
        var user = await _testData.AddMemberAsync(later.Id);
        await _testData.AddMemberAsync(earlier.Id, user);

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);

        Tenants(page).ShouldBe([(earlier.Id, "Same Ltd", earlier.Slug), (later.Id, "Same Ltd", later.Slug)]);
    }

    // The member of a suspended tenant still sees it, with its status, so a client can tell them why they cannot enter it.
    [Fact]
    public async Task ListMyTenants_ASuspendedTenant_IsListedWithItsStatus()
    {
        var active = await _testData.AddTenantAsync(name: "Active Ltd");
        var suspended = await _testData.AddTenantAsync(status: "Suspended", name: "Suspended Ltd");
        var user = await _testData.AddMemberAsync(active.Id);
        await _testData.AddMemberAsync(suspended.Id, user);

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("status").GetString()))
            .ShouldBe([(active.Id, "active"), (suspended.Id, "suspended")]);
        page.GetProperty("total").GetInt32().ShouldBe(2);
    }

    // Someone signed in with the identity provider who has never joined a tenant is not a user yet.
    [Fact]
    public async Task ListMyTenants_ASignedInPersonWhoIsNotAUserYet_ReturnsAnEmptyList()
    {
        var response = await _api.CreateClient($"user_{Guid.NewGuid():N}").GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Tenants(page).ShouldBeEmpty();
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ListMyTenants_WithoutAToken_IsUnauthorized()
    {
        var response = await _api.CreateClient().GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListMyTenants_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        var first = await _testData.AddTenantAsync(name: "First Ltd");
        var second = await _testData.AddTenantAsync(name: "Second Ltd");
        var user = await _testData.AddMemberAsync(first.Id);
        await _testData.AddMemberAsync(second.Id, user);

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/tenants?page=2&pageSize=1", Cancellation);

        Tenants(page).ShouldBe([(second.Id, "Second Ltd", second.Slug)]);
        page.GetProperty("total").GetInt32().ShouldBe(2);
    }

    [Theory]
    [InlineData("pageSize=101", "paging.page_size_invalid")]
    [InlineData("page=0", "paging.page_invalid")]
    public async Task ListMyTenants_PagingOutsideItsRange_IsRejected(string query, string code)
    {
        var user = await _testData.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/me/tenants?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    [Theory]
    [InlineData("acme", "Acme Global")]
    [InlineData("ACME gl", "Acme Global")]
    [InlineData("orbit", "Bravo Ltd")]
    [InlineData("ORBIT-", "Bravo Ltd")]
    public async Task ListMyTenants_ASearch_FindsTheTenantsWhoseNameOrSlugContainsItInAnyCase(string q, string found)
    {
        var random = Guid.NewGuid().ToString("N")[..8];
        var user = await MemberOfAsync(
            await _testData.AddTenantAsync(name: "Acme Global", slug: $"alpha-{random}"),
            await _testData.AddTenantAsync(name: "Bravo Ltd", slug: $"orbit-{random}"),
            await _testData.AddTenantAsync(name: "Charlie Ltd", slug: $"charlie-{random}"));

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>($"/v1/me/tenants?q={Uri.EscapeDataString(q)}", Cancellation);

        Tenants(page).Select(tenant => tenant.Name).ShouldBe([found]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("0%", "100% Ltd")]
    [InlineData("a_b", "a_b Ltd")]
    [InlineData(@"c\d", @"c\d Ltd")]
    public async Task ListMyTenants_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        var user = await MemberOfAsync(
            await _testData.AddTenantAsync(name: "100% Ltd"),
            await _testData.AddTenantAsync(name: "1000 Ltd"),
            await _testData.AddTenantAsync(name: "a_b Ltd"),
            await _testData.AddTenantAsync(name: "axb Ltd"),
            await _testData.AddTenantAsync(name: @"c\d Ltd"),
            await _testData.AddTenantAsync(name: "cd Ltd"));

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>($"/v1/me/tenants?q={Uri.EscapeDataString(q)}", Cancellation);

        Tenants(page).Select(tenant => tenant.Name).ShouldBe([found]);
    }

    [Theory]
    [InlineData("name", "Alpha Ltd, Bravo Ltd, Charlie Ltd")]
    [InlineData("-name", "Charlie Ltd, Bravo Ltd, Alpha Ltd")]
    public async Task ListMyTenants_ASortField_OrdersByItInItsDirection(string sort, string names)
    {
        var user = await MemberOfAsync(
            await _testData.AddTenantAsync(name: "Bravo Ltd"),
            await _testData.AddTenantAsync(name: "Alpha Ltd"),
            await _testData.AddTenantAsync(name: "Charlie Ltd"));

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>($"/v1/me/tenants?sort={sort}", Cancellation);

        string.Join(", ", Tenants(page).Select(tenant => tenant.Name)).ShouldBe(names);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("-name")]
    public async Task ListMyTenants_TwoTenantsWithTheSameSortValue_KeepTheirOrderAcrossPages(string sort)
    {
        var random = Guid.NewGuid().ToString();
        var later = await _testData.AddTenantAsync(name: "Same Ltd", tenantId: Guid.Parse($"f{random[1..]}"));
        var earlier = await _testData.AddTenantAsync(name: "Same Ltd", tenantId: Guid.Parse($"0{random[1..]}"));
        var client = _api.CreateClient(await MemberOfAsync(later, earlier));

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/me/tenants?sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await client.GetFromJsonAsync<JsonElement>($"/v1/me/tenants?sort={sort}&pageSize=1&page=2", Cancellation);

        Tenants(first).Concat(Tenants(second)).Select(tenant => tenant.Id).ShouldBe([earlier.Id, later.Id]);
    }

    [Theory]
    [InlineData("sort=age", "paging.sort_invalid")]
    [InlineData("sort=slug", "paging.sort_invalid")]
    [InlineData("sort=-Name", "paging.sort_invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListMyTenants_AnUnknownSortOrALongSearch_IsRejected(string query, string code)
    {
        var user = await _testData.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/me/tenants?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    // T4: a member still finds a closed tenant in their list, with its status, as a suspended one.
    [Fact]
    public async Task ListMyTenants_MemberOfAClosedTenant_ListsItAsClosed()
    {
        var tenant = await _testData.AddTenantAsync(name: "Closed Ltd");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await (await SystemAdminAsync()).CloseTenantAsync(tenant.Id);

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);

        var listed = page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString(), item.GetProperty("status").GetString()));
        listed.ShouldBe([(tenant.Id, "Closed Ltd", "closed")]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "role"], ignoreOrder: true);
    }

    private static List<(Guid Id, string Name, string Slug)> Tenants(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString()!, item.GetProperty("slug").GetString()!)),
    ];

    // A new user who is a member of each tenant.
    private async Task<string> MemberOfAsync(params (Guid Id, string Slug)[] tenants)
    {
        var user = await _testData.AddUserAsync();
        foreach (var tenant in tenants)
        {
            await _testData.AddMemberAsync(tenant.Id, user);
        }

        return user;
    }

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
