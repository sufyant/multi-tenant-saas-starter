using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// GET /v1/tenants/{tenantId}/invitations: the tenant's pending invitations, expired ones included, through the list contract (K5).
public sealed class ListInvitationsTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Soon = DateTimeOffset.UtcNow.AddDays(1);

    private readonly Catalog _catalog = new(database);
    // A whole second: PostgreSQL rounds a stored time to microseconds, so a clock with ticks could store an expiry after the clock's.
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // An expired invitation stays in the list; a cancelled or accepted one is gone from it.
    [Fact]
    public async Task ListInvitations_AsAnOwner_ReturnsThePendingInvitationsOfThatTenantOnlyExpiredOnesIncluded()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var expired = await _catalog.AddInvitationAsync(tenant.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        var pending = await _catalog.AddInvitationAsync(tenant.Id, role: "Admin", expiresAt: Soon);
        await _catalog.AddInvitationAsync(tenant.Id, status: "Cancelled");
        await _catalog.AddInvitationAsync(tenant.Id, status: "Accepted");
        await _catalog.AddInvitationAsync(other.Id);

        var response = await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant.Id}/invitations", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Invitations(page).ShouldBe([(expired, Roles.BuiltIn("member")), (pending, Roles.BuiltIn("admin"))]);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(50);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("items")[0].EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "role", "invitedBy", "invitedByName", "createdAt", "expiresAt", "state"], ignoreOrder: true);
    }

    // The API answers the state from the invitation's expiry by its own clock (Invitation.StateAt), so the screen never reads it from the
    // browser's clock. A pending invitation is pending until its expiry, and expired from that moment on.
    [Fact]
    public async Task ListInvitations_APendingInvitationAtItsExpiryByTheClock_TurnsFromPendingToExpired()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, expiresAt: _time.GetUtcNow().AddDays(1));
        var url = $"/v1/tenants/{tenant.Id}/invitations";

        var before = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>(url, Cancellation);
        _time.Advance(TimeSpan.FromDays(1));
        var after = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>(url, Cancellation);

        before.GetProperty("items")[0].GetProperty("state").GetString().ShouldBe("pending");
        after.GetProperty("items")[0].GetProperty("state").GetString().ShouldBe("expired");
    }

    // The inviter's name is resolved on read, as the audit names are (K3): a rename shows, and a deleted inviter has no name and says
    // deleted. invitedBy stays the catalog user id.
    [Fact]
    public async Task ListInvitations_EachInvitation_NamesItsInviterAsTheCatalogNamesThemNow()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var ada = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(firstName: "Ada", lastName: "Byron"), role: "Admin");
        var gone = await _catalog.AddUserAsync(firstName: "Gone", lastName: "Away");
        var byAda = await _catalog.AddInvitationAsync(tenant.Id, email: "a@example.com", invitedBy: ada);
        var byGone = await _catalog.AddInvitationAsync(tenant.Id, email: "b@example.com", invitedBy: gone);
        await database.ScalarAsync<object>($"UPDATE control_plane.users SET last_name = 'Lovelace' WHERE external_id = '{ada}'");
        await _catalog.DeleteUserAsync(gone);

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?sort=email", Cancellation);

        page.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("invitedBy").GetGuid(), item.GetProperty("invitedByName").ToString()))
            .ShouldBe(
            [
                (byAda, await _catalog.UserIdOfAsync(ada), """{"firstName":"Ada","lastName":"Lovelace","deleted":false}"""),
                (byGone, await _catalog.UserIdOfAsync(gone), """{"firstName":null,"lastName":null,"deleted":true}"""),
            ]);
    }

    // The page is one query, with each inviter's user joined: a longer page sends no more commands (K5).
    [Fact]
    public async Task ListInvitations_ALongerPage_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await AddInvitationsAsync(tenant.Id, "a@example.com", "b@example.com", "c@example.com");
        var url = $"/v1/tenants/{tenant.Id}/invitations";

        var one = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=1", Cancellation));
        var three = await commands.CountAsync(() => _api.CreateClient(owner), client => client.GetAsync($"{url}?pageSize=3", Cancellation));

        three.ShouldBe(one);
        one.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListInvitations_AsAnAdmin_IsAllowed()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddMemberAsync(tenant.Id, role: "Admin");

        var response = await _api.CreateClient(admin).GetAsync($"/v1/tenants/{tenant.Id}/invitations", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListInvitations_AsAMember_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/invitations", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Row level security keeps the list to the tenant of the path: a search that only another tenant's invitation matches finds none.
    [Fact]
    public async Task ListInvitations_ASearchThatMatchesOnlyAnotherTenantsInvitation_FindsNone()
    {
        var needle = $"needle-{Guid.NewGuid():N}@example.com";
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(other.Id, needle);

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?q={Uri.EscapeDataString(needle)}", Cancellation);

        Invitations(page).ShouldBeEmpty();
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("MATH.org", "ada@math.org")]
    [InlineData("GRACE", "grace@navy.mil")]
    public async Task ListInvitations_ASearch_FindsTheInvitationsWhoseEmailContainsItInAnyCase(string q, string found)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, "ada@math.org");
        await _catalog.AddInvitationAsync(tenant.Id, "grace@navy.mil");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?q={Uri.EscapeDataString(q)}", Cancellation);

        Emails(page).ShouldBe([found]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("r%c", "per%cent@t.org")]
    [InlineData("r_s", "under_score@t.org")]
    [InlineData(@"k\s", @"back\slash@t.org")]
    public async Task ListInvitations_ASearchWithAWildcard_MatchesTheCharacterItself(string q, string found)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await AddInvitationsAsync(tenant.Id, "per%cent@t.org", "perxcent@t.org", "under_score@t.org", "underxscore@t.org", @"back\slash@t.org", "backslash@t.org");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?q={Uri.EscapeDataString(q)}", Cancellation);

        Emails(page).ShouldBe([found]);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    public async Task ListInvitations_ARoleFilter_ReturnsTheInvitationsOfThatRoleOnly(string role)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, role: "Admin");
        await _catalog.AddInvitationAsync(tenant.Id, role: "Member");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?role={role}", Cancellation);

        Invitations(page).Select(invitation => invitation.Role).ShouldBe([Roles.BuiltIn(role)]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("", "b@t.org, c@t.org, a@t.org")]
    [InlineData("expiresAt", "b@t.org, c@t.org, a@t.org")]
    [InlineData("-expiresAt", "a@t.org, c@t.org, b@t.org")]
    [InlineData("email", "a@t.org, b@t.org, c@t.org")]
    [InlineData("-email", "c@t.org, b@t.org, a@t.org")]
    public async Task ListInvitations_ASortField_OrdersByItInItsDirection(string sort, string emails)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, "a@t.org", expiresAt: Soon.AddHours(3));
        await _catalog.AddInvitationAsync(tenant.Id, "b@t.org", expiresAt: Soon.AddHours(1));
        await _catalog.AddInvitationAsync(tenant.Id, "c@t.org", expiresAt: Soon.AddHours(2));

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?sort={sort}", Cancellation);

        string.Join(", ", Emails(page)).ShouldBe(emails);
    }

    // Two pending invitations never share an email address, so only the expiry can tie; the id breaks the tie (K5).
    [Theory]
    [InlineData("expiresAt")]
    [InlineData("-expiresAt")]
    public async Task ListInvitations_TwoInvitationsWithTheSameExpiry_KeepTheirOrderAcrossPages(string sort)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var random = Guid.NewGuid().ToString();
        var (lower, higher) = (Guid.Parse($"0{random[1..]}"), Guid.Parse($"f{random[1..]}"));
        await _catalog.AddInvitationAsync(tenant.Id, expiresAt: Soon, id: higher);
        await _catalog.AddInvitationAsync(tenant.Id, expiresAt: Soon, id: lower);
        var client = _api.CreateClient(owner);

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?sort={sort}&pageSize=1&page=1", Cancellation);
        var second = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?sort={sort}&pageSize=1&page=2", Cancellation);

        Invitations(first).Concat(Invitations(second)).Select(invitation => invitation.Id).ShouldBe([lower, higher]);
    }

    [Fact]
    public async Task ListInvitations_ALaterPage_ReturnsTheRestAndTheTotal()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await _catalog.AddInvitationAsync(tenant.Id, "a@t.org");
        await _catalog.AddInvitationAsync(tenant.Id, "b@t.org");
        await _catalog.AddInvitationAsync(tenant.Id, "c@t.org");

        var page = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/invitations?sort=email&page=2&pageSize=2", Cancellation);

        Emails(page).ShouldBe(["c@t.org"]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData("pageSize=101", "paging.page_size_invalid")]
    [InlineData("pageSize=0", "paging.page_size_invalid")]
    [InlineData("page=0", "paging.page_invalid")]
    [InlineData("sort=createdAt", "paging.sort_invalid")]
    [InlineData("sort=ExpiresAt", "paging.sort_invalid")]
    [InlineData("role=Owner", "paging.filter_invalid")]
    [InlineData("role=Guest", "paging.filter_invalid")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "paging.q_invalid")]
    public async Task ListInvitations_AnInvalidPageSortFilterOrSearch_IsRejected(string query, string code)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner).GetAsync($"/v1/tenants/{tenant.Id}/invitations?{query}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"].ShouldNotBeNull().ToString().ShouldBe(code);
    }

    private async Task AddInvitationsAsync(Guid tenantId, params string[] emails)
    {
        foreach (var email in emails)
        {
            await _catalog.AddInvitationAsync(tenantId, email);
        }
    }

    private static List<string> Emails(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("email").GetString()!)];

    private static List<(Guid Id, string Role)> Invitations(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("id").GetGuid(), Roles.Of(item)))];
}
