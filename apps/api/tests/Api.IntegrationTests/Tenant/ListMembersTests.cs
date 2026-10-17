using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// GET /v1/tenants/{tenantId}/members, the read endpoint behind the tenant door: the door, membership, row level security and the
// permission check, through the composed application.
public sealed class ListMembersTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ListMembers_AsAMember_ReturnsTheMembersOfThatTenantOnly()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var admin = await _catalog.AddMemberAsync(tenant.Id, role: "Admin");
        await _catalog.AddMemberAsync(other.Id, role: "Owner");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Members(page).ShouldBe(
        [
            (await _catalog.UserIdOfAsync(owner), "owner"),
            (await _catalog.UserIdOfAsync(admin), "admin"),
            (await _catalog.UserIdOfAsync(member), "member"),
        ]);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task ListMembers_OfTheSameRole_AreOrderedByUserId()
    {
        var tenant = await _catalog.AddTenantAsync();
        var random = Guid.NewGuid().ToString();
        var (lower, higher) = (Guid.Parse($"0{random[1..]}"), Guid.Parse($"f{random[1..]}"));
        var later = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(higher));
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(lower));

        var page = await _api.CreateClient(later).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members", Cancellation);

        Members(page).ShouldBe([(lower, "member"), (higher, "member")]);
    }

    // T2: the tenant comes from the path, whatever the query string or a header says.
    [Fact]
    public async Task ListMembers_AnotherTenantInTheQueryAndAHeader_ReturnsTheTenantOfThePath()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(other.Id, caller, role: "Member");
        await _catalog.AddMemberAsync(other.Id, role: "Owner");
        var client = _api.CreateClient(caller);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", other.Id.ToString());

        var page = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?tenantId={other.Id}", Cancellation);

        Members(page).ShouldBe([(await _catalog.UserIdOfAsync(caller), "owner")]);
    }

    // T3: a tenant the caller cannot enter looks the same as one that does not exist.
    [Fact]
    public async Task ListMembers_AsAUserWhoIsNotAMember_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var outsider = await _catalog.AddUserAsync();

        var response = await _api.CreateClient(outsider).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMembers_UnknownTenantId_IsNotFound()
    {
        var user = await _catalog.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/tenants/{Guid.NewGuid()}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMembers_TenantIdThatIsNotAGuid_IsNotFound()
    {
        var user = await _catalog.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync("/v1/tenants/not-a-guid/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMembers_TenantThatIsNotActive_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync(status: "Provisioning");
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // T5: a system admin is not a member, so the tenant door does not let them in.
    [Fact]
    public async Task ListMembers_AsASystemAdminWhoIsNotAMember_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddSystemAdminAsync();

        var response = await _api.CreateClient(admin, secondFactor: true).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // T1: the slug is never part of a path.
    [Fact]
    public async Task ListMembers_TheSlugInThePath_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Slug}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMembers_WithoutAToken_IsUnauthorized()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await _api.CreateClient().GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListMembers_TheMaximumPageSize_IsAccepted()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?pageSize=100", Cancellation);

        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task ListMembers_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(tenant.Id, role: "Admin");
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?page=2&pageSize=2", Cancellation);

        Members(page).ShouldBe([(await _catalog.UserIdOfAsync(member), "member")]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101", "paging.page_size_invalid")]
    [InlineData("pageSize=0", "paging.page_size_invalid")]
    [InlineData("page=0", "paging.page_invalid")]
    public async Task ListMembers_PagingOutsideItsRange_IsRejected(string query, string code)
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    // The email address and the names come from the catalog's copy of each user's profile (A7).
    [Fact]
    public async Task ListMembers_AsAMember_ReturnsEachMemberWithTheirEmailAndNames()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "ada@math.org", firstName: "Ada", lastName: "Lovelace"), role: "Owner");
        var member = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "grace@navy.mil", firstName: "Grace"));

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Profiles(page).ShouldBe(
        [
            (await _catalog.UserIdOfAsync(owner), "ada@math.org", "Ada", "Lovelace", "owner"),
            (await _catalog.UserIdOfAsync(member), "grace@navy.mil", "Grace", null, "member"),
        ]);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["userId", "email", "firstName", "lastName", "role", "deleted", "joinedAt"], ignoreOrder: true);
        Deleted(page).ShouldBe([false, false]);
    }

    // The Members screen shows when each member joined (memberships.joined_at).
    [Fact]
    public async Task ListMembers_EachMember_ReturnsWhenTheyJoined()
    {
        var tenant = await _catalog.AddTenantAsync();
        var first = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner", joinedAt: first);
        var member = await _catalog.AddMemberAsync(tenant.Id, joinedAt: later);

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members", Cancellation);

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("userId").GetGuid(), item.GetProperty("joinedAt").GetDateTimeOffset()))
            .ShouldBe([(await _catalog.UserIdOfAsync(owner), first), (await _catalog.UserIdOfAsync(member), later)]);
    }

    // The page is one query, with each member's user and role joined: a longer page sends no more commands (K5).
    [Fact]
    public async Task ListMembers_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await AddMembersAsync(tenant.Id, "Ball", "Cole");
        var url = $"/v1/tenants/{tenant.Id}/members";

        var one = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=1", Cancellation));
        var three = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    // The screen shows "Deleted user", as for audit (K3): the member says deleted, as the audit logs do.
    [Fact]
    public async Task ListMembers_AMemberWhoseUserWasDeleted_StaysWithoutEmailOrNamesAndSaysDeleted()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var deleted = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "gone@example.com", firstName: "Gone", lastName: "Away"));
        await _catalog.DeleteUserAsync(deleted);

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?role=member", Cancellation);

        Profiles(page).ShouldBe([(await _catalog.UserIdOfAsync(deleted), null, null, null, "member")]);
        Deleted(page).ShouldBe([true]);
    }

    // R6: the users table is not under row level security. The search starts from the active tenant's memberships, so a user of
    // another tenant whose email address matches stays out of the list.
    [Fact]
    public async Task ListMembers_ASearchThatMatchesOnlyAUserOfAnotherTenant_FindsNoOne()
    {
        var needle = $"needle-{Guid.NewGuid():N}@example.com";
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(other.Id, await _catalog.AddUserAsync(email: needle), role: "Owner");

        var page = await _api.CreateClient(caller).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?q={Uri.EscapeDataString(needle)}", Cancellation);

        Members(page).ShouldBeEmpty();
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("MATH.org", "m1@math.org")]
    [InlineData("grace", "m2@navy.mil")]
    [InlineData("LOVEL", "m1@math.org")]
    public async Task ListMembers_ASearch_FindsTheMembersWhoseEmailOrNameContainsItInAnyCase(string q, string found)
    {
        var tenant = await _catalog.AddTenantAsync();
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "m1@math.org", firstName: "Ada", lastName: "Lovelace"));
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "m2@navy.mil", firstName: "Grace", lastName: "Hopper"));

        var page = await _api.CreateClient(caller).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?q={Uri.EscapeDataString(q)}", Cancellation);

        Profiles(page).Select(member => member.Email).ShouldBe([found]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("r%c", "Per%cent")]
    [InlineData("r_s", "Under_score")]
    [InlineData(@"k\s", @"Back\slash")]
    public async Task ListMembers_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        var tenant = await _catalog.AddTenantAsync();
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await AddMembersAsync(tenant.Id, "Per%cent", "Perxcent", "Under_score", "Underxscore", @"Back\slash", "Backslash");

        var page = await _api.CreateClient(caller).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?q={Uri.EscapeDataString(q)}", Cancellation);

        Profiles(page).Select(member => member.LastName).ShouldBe([found]);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    public async Task ListMembers_ARoleFilter_ReturnsTheMembersOfThatRoleOnly(string role)
    {
        var tenant = await _catalog.AddTenantAsync();
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(tenant.Id, role: "Admin");
        await _catalog.AddMemberAsync(tenant.Id, role: "Member");

        var page = await _api.CreateClient(caller).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?role={role}", Cancellation);

        Members(page).Select(member => member.Role).ShouldBe([role]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    // A member without a last name comes last in both directions, as one without an email address does.
    [Theory]
    [InlineData("role", "c@t.org, a@t.org, b@t.org")]
    [InlineData("-role", "b@t.org, a@t.org, c@t.org")]
    [InlineData("email", "a@t.org, b@t.org, c@t.org")]
    [InlineData("-email", "c@t.org, b@t.org, a@t.org")]
    [InlineData("lastName", "c@t.org, b@t.org, a@t.org")]
    [InlineData("-lastName", "b@t.org, c@t.org, a@t.org")]
    public async Task ListMembers_ASortField_OrdersByItInItsDirection(string sort, string emails)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "c@t.org", lastName: "Young"), role: "Owner");
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "a@t.org"), role: "Admin");
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "b@t.org", lastName: "Zed"), role: "Member");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?sort={sort}", Cancellation);

        string.Join(", ", Profiles(page).Select(member => member.Email)).ShouldBe(emails);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("-role")]
    [InlineData("email")]
    [InlineData("-email")]
    [InlineData("lastName")]
    [InlineData("-lastName")]
    public async Task ListMembers_TwoMembersWithTheSameSortValue_KeepTheirOrderAcrossPages(string sort)
    {
        var tenant = await _catalog.AddTenantAsync();
        var random = Guid.NewGuid().ToString();
        var (lower, higher) = (Guid.Parse($"0{random[1..]}"), Guid.Parse($"f{random[1..]}"));
        var caller = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(higher, email: "same@t.org", lastName: "Same"));
        await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(lower, email: "same@t.org", lastName: "Same"));
        var client = _api.CreateClient(caller);

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?role=member&sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/members?role=member&sort={sort}&pageSize=1&page=2", Cancellation);

        Members(first).Concat(Members(second)).Select(member => member.UserId).ShouldBe([lower, higher]);
    }

    [Theory]
    [InlineData("sort=age", "paging.sort_invalid")]
    [InlineData("sort=Role", "paging.sort_invalid")]
    [InlineData("sort=userId", "paging.sort_invalid")]
    [InlineData("role=Owner", "paging.filter_invalid")]
    [InlineData("role=Guest", "paging.filter_invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListMembers_AnUnknownSortOrFilterValueOrALongSearch_IsRejected(string query, string code)
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe(code);
    }

    private async Task AddMembersAsync(Guid tenantId, params string[] lastNames)
    {
        foreach (var lastName in lastNames)
        {
            await _catalog.AddMemberAsync(tenantId, await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", lastName: lastName));
        }
    }

    private static List<(Guid UserId, string? Email, string? FirstName, string? LastName, string Role)> Profiles(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("userId").GetGuid(),
            item.GetProperty("email").GetString(),
            item.GetProperty("firstName").GetString(),
            item.GetProperty("lastName").GetString(),
            item.GetProperty("role").GetString()!)),
    ];

    private static List<bool> Deleted(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("deleted").GetBoolean())];

    private static List<(Guid UserId, string Role)> Members(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("userId").GetGuid(), item.GetProperty("role").GetString()!))];
}
